using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqQueryIndex
{
    public class IndexExpressionRewriter : ExpressionVisitor
    {
		private static readonly Type _groupingType = typeof(Lookup<,>).Assembly.GetType("System.Linq.Lookup`2+Grouping");
		private static readonly Lazy<ILookup<string, MethodInfo>> _enumerableMethods = new Lazy<ILookup<string, MethodInfo>>(() =>
			typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).ToLookup(m => m.Name));
		private readonly Lazy<ILookup<string, MethodInfo>> _indexMethods;

		internal IndexQueryValues _index;
		private readonly Expression _indexExpression;

		internal IndexExpressionRewriter(IndexQueryValues index, Expression indexExpression)
		{
			_index = index ?? throw new ArgumentNullException(nameof(index));
			_indexExpression = indexExpression ?? throw new ArgumentNullException(nameof(indexExpression));
			_indexMethods = new Lazy<ILookup<string, MethodInfo>>(() =>
				typeof(Index<>).MakeGenericType(index.ElementType).GetMethods(BindingFlags.Instance | BindingFlags.Public).ToLookup(m => m.Name));
		}

        protected override Expression VisitMethodCall(MethodCallExpression call)
		{
            if (call is null)
            {
                throw new ArgumentNullException(nameof(call));
            }

            var expression = Visit(call.Object);
			var arguments = VisitExpressionList(call.Arguments);
			var typeArgs = call.Method.IsGenericMethod ? call.Method.GetGenericArguments() : null;
			if (call.Method.DeclaringType == typeof(Queryable))
			{
				return TurnIntoEnumerableMethod(call, expression, arguments, typeArgs);
			}
			if (expression == call.Object && arguments == call.Arguments)
			{
				return call;
			}
			if ((call.Method.IsStatic || call.Method.DeclaringType.IsAssignableFrom(expression.Type)) && ArgsMatch(call.Method, arguments, typeArgs))
			{
				return Expression.Call(expression, call.Method, arguments);
			}
			var flags = BindingFlags.Static | (call.Method.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic);
			var methodInfo2 = FindMethod(call.Method.DeclaringType, call.Method.Name, arguments, typeArgs, flags);
			arguments = FixupQuotedArgs(methodInfo2, arguments);
			return Expression.Call(expression, methodInfo2, arguments);
		}

		private Expression TurnIntoEnumerableMethod(MethodCallExpression call, Expression expression, IReadOnlyList<Expression> arguments, Type[] genericTypes)
		{
			var enumerableMethod = FindEnumerableOrIndexMethod(call.Method.Name, _enumerableMethods.Value, arguments, genericTypes);
			arguments = FixupQuotedArgs(enumerableMethod, arguments);
			Index index;
			(PropertyInfo Property, Expression PropertyValue)? info = default;
			if (enumerableMethod.IsGenericMethod &&
				call.Arguments.Count > 0 &&
				(call.Arguments[0] == _indexExpression || FindGenericType(typeof(IndexQuery<>), call.Arguments[0].Type) != null) &&
				(info = TryGetPropertyAndValue(arguments[1])) != null &&
				(index = _index.Indexes.FirstOrDefault(i => i.Property == info.Value.Property)) != null)
			{
				var indexArguments = new List<Expression>();
				if (info.Value.PropertyValue != null)
				{
					indexArguments.Add(info.Value.PropertyValue);
				}
				indexArguments.AddRange(arguments.Skip(2));
				var comparer = ExtractComparer(indexArguments);
				if (comparer == null || index.Matches(info.Value.Property, comparer))
				{
					var indexGenericArguments = (Type[])genericTypes.Clone();
					indexGenericArguments[indexGenericArguments.Length - 1] = info.Value.Property.PropertyType;
					var indexMethod = FindEnumerableOrIndexMethod(call.Method.Name, _indexMethods.Value, indexArguments, indexGenericArguments, throwIfNoMethod: false);
					if (indexMethod != null)
					{
						return Expression.Call(
							Expression.Constant(index),
							indexMethod,
							indexArguments);
					}
				}
			}
			return Expression.Call(expression, enumerableMethod, arguments);
		}

		private static object ExtractComparer(IList<Expression> arguments)
		{
			for (int i = 0; i < arguments.Count; i++)
			{
				var comparer = FindGenericType(typeof(IEqualityComparer<>), arguments[i].Type);
				if (comparer != null)
				{
					var lambda = Expression.Lambda<Func<object>>(Expression.Convert(arguments[i], typeof(object))).Compile();
					arguments.RemoveAt(i);
					return lambda();
				}
			}
			return null;
		}

		private static (PropertyInfo Property, Expression PropertyValue)? TryGetPropertyAndValue(Expression expression)
		{
			// o => o.Property == "value" (for Where, First... expressions)
			if (expression is LambdaExpression filterLambda && filterLambda.Body is BinaryExpression binary &&
				binary.NodeType == ExpressionType.Equal &&
				binary.Left is MemberExpression member &&
				member.Member is PropertyInfo property)
			{
				return (property, binary.Right);
			}

			// o => o.Property (for GroupBy expressions)
			if (expression is LambdaExpression properytLambda && properytLambda.Body is MemberExpression propertyExpression &&
				propertyExpression.Member is PropertyInfo propertyExpressionMember)
			{
				return (propertyExpressionMember, null);
			}

			return null;
		}

		private IReadOnlyList<Expression> VisitExpressionList(IReadOnlyList<Expression> original)
		{
			List<Expression> list = null;
			int i = 0;
			int count = original.Count;
			while (i < count)
			{
				Expression expression = this.Visit(original[i]);
				if (list != null)
				{
					list.Add(expression);
				}
				else if (expression != original[i])
				{
					list = new List<Expression>(count);
					for (int j = 0; j < i; j++)
					{
						list.Add(original[j]);
					}
					list.Add(expression);
				}
				i++;
			}
			return list ?? original;
		}

		private IReadOnlyList<Expression> FixupQuotedArgs(MethodInfo method, IReadOnlyList<Expression> argList)
		{
			List<Expression> list = null;
			var parameters = method.GetParameters();
			if (parameters.Length != 0)
			{
				var i = 0;
				var num = parameters.Length;
				while (i < num)
				{
					var expression = argList[i];
					var parameterInfo = parameters[i];
					expression = FixupQuotedExpression(parameterInfo.ParameterType, expression);
					if (list == null && expression != argList[i])
					{
						list = new List<Expression>(argList.Count);
						for (int j = 0; j < i; j++)
						{
							list.Add(argList[j]);
						}
					}
					if (list != null)
					{
						list.Add(expression);
					}
					i++;
				}
			}
			return list ?? argList;
		}

		private Expression FixupQuotedExpression(Type type, Expression expression)
		{
			var expression2 = expression;
			while (!type.IsAssignableFrom(expression2.Type))
			{
				if (expression2.NodeType != ExpressionType.Quote)
				{
					if (!type.IsAssignableFrom(expression2.Type) && type.IsArray && expression2.NodeType == ExpressionType.NewArrayInit)
					{
						var c = StripExpression(expression2.Type);
						if (type.IsAssignableFrom(c))
						{
							var elementType = type.GetElementType();
							var newArrayExpression = (NewArrayExpression)expression2;
							var list = new List<Expression>(newArrayExpression.Expressions.Count);
							var i = 0;
							var count = newArrayExpression.Expressions.Count;
							while (i < count)
							{
								list.Add(FixupQuotedExpression(elementType, newArrayExpression.Expressions[i]));
								i++;
							}
							expression = Expression.NewArrayInit(elementType, list);
						}
					}
					return expression;
				}
				expression2 = ((UnaryExpression)expression2).Operand;
			}
			return expression2;
		}

		protected override Expression VisitLambda<T>(Expression<T> lambda)
		{
			return lambda;
		}

		private static Type GetPublicType(Type t)
		{
			if (t.IsGenericType && t.GetGenericTypeDefinition() == _groupingType)
			{
				return typeof(IGrouping<,>).MakeGenericType(t.GetGenericArguments());
			}
			if (!t.IsNestedPrivate)
			{
				return t;
			}
			foreach (Type type in t.GetInterfaces())
			{
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				{
					return type;
				}
			}
			if (typeof(IEnumerable).IsAssignableFrom(t))
			{
				return typeof(IEnumerable);
			}
			return t;
		}

		protected override Expression VisitConstant(ConstantExpression c)
		{
			var query = c?.Value as IndexQuery;
			if (query == null)
			{
				return c;
			}
			if (query.Enumerable != null)
			{
				var publicType = GetPublicType(query.Enumerable.GetType());
				return Expression.Constant(query.Enumerable, publicType);
			}
			return Visit(query.Expression);
		}

		protected override Expression VisitParameter(ParameterExpression p)
		{
			return p;
		}

		private static MethodInfo FindEnumerableOrIndexMethod(string name, ILookup<string, MethodInfo> methods, IReadOnlyList<Expression> args, Type[] genericTypes, bool throwIfNoMethod = true)
		{
			var methodInfo = methods[name].FirstOrDefault(m => ArgsMatch(m, args, genericTypes));
			if (methodInfo == null)
			{
				if (throwIfNoMethod)
				{
					throw new InvalidOperationException($"No suitable method '{name}' could be found.");
				}
				return null;
			}
			if (genericTypes != null)
			{
				return methodInfo.MakeGenericMethod(genericTypes);
			}
			return methodInfo;
		}

		internal static MethodInfo FindMethod(Type type, string name, IReadOnlyList<Expression> args, Type[] genericTypes, BindingFlags flags)
		{
			var method = (from m in type.GetMethods(flags)
							  where m.Name == name
							  where ArgsMatch(m, args, genericTypes)
							  select m).FirstOrDefault();
			if (method == null)
			{
				throw new InvalidOperationException($"No suitable method '{name}' could be found.");
			}
			if (genericTypes != null)
			{
				return method.MakeGenericMethod(genericTypes);
			}
			return method;
		}

		private static bool ArgsMatch(MethodInfo m, IReadOnlyList<Expression> args, Type[] genericTypes)
		{
			var parameters = m.GetParameters();
			if (parameters.Length != args.Count)
			{
				return false;
			}
			if (!m.IsGenericMethod && genericTypes != null && genericTypes.Length != 0)
			{
				return false;
			}
			if (!m.IsGenericMethodDefinition && m.IsGenericMethod && m.ContainsGenericParameters)
			{
				m = m.GetGenericMethodDefinition();
			}
			if (m.IsGenericMethodDefinition)
			{
				if (genericTypes == null || genericTypes.Length == 0)
				{
					return false;
				}
				if (m.GetGenericArguments().Length != genericTypes.Length)
				{
					return false;
				}
				m = m.MakeGenericMethod(genericTypes);
				parameters = m.GetParameters();
			}
			int i = 0;
			int count = args.Count;
			while (i < count)
			{
				Type type = parameters[i].ParameterType;
				if (type == null)
				{
					return false;
				}
				if (type.IsByRef)
				{
					type = type.GetElementType();
				}
				var expression = args[i];
				if (!type.IsAssignableFrom(expression.Type))
				{
					if (expression.NodeType == ExpressionType.Quote)
					{
						expression = ((UnaryExpression)expression).Operand;
					}
					if (!type.IsAssignableFrom(expression.Type) && !type.IsAssignableFrom(StripExpression(expression.Type)))
					{
						return false;
					}
				}
				i++;
			}
			return true;
		}

		private static Type StripExpression(Type type)
		{
			var isArray = type.IsArray;
			var elementType = isArray ? type.GetElementType() : type;
			var genericType = FindGenericType(typeof(Expression<>), elementType);
			if (genericType != null)
			{
				elementType = genericType.GetGenericArguments()[0];
			}
			if (!isArray)
			{
				return type;
			}
			int arrayRank = type.GetArrayRank();
			if (arrayRank != 1)
			{
				return elementType.MakeArrayType(arrayRank);
			}
			return elementType.MakeArrayType();
		}

		internal static Type FindGenericType(Type definition, Type type)
		{
			while (type != null && type != typeof(object))
			{
				if (type.IsGenericType && type.GetGenericTypeDefinition() == definition)
				{
					return type;
				}
				if (definition.IsInterface)
				{
					foreach (var @interface in type.GetInterfaces())
					{
						var genericType = FindGenericType(definition, @interface);
						if (genericType != null)
						{
							return genericType;
						}
					}
				}
				type = type.BaseType;
			}
			return null;
		}
	}
}
