using System.Net;

namespace Shockzam
{
    public struct RequestContext
    {
        public IRequestBuilder RequestBuilder { get; set; }

        public object State { get; set; }

        public WebRequest WebRequest { get; set; }
    }
}