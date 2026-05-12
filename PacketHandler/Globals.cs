
global using H = PacketHandlerUtils;

// To log H.Dump object name
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public string ParameterName { get; }

        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
}