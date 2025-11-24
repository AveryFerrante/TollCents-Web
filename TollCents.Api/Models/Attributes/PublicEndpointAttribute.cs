namespace TollCents.Api.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PublicEndpointAttribute : Attribute { }
}
