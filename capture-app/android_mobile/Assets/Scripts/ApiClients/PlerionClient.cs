using System.Net.Http;
using System.Net.Http.Headers;

namespace PlerionClient.Client
{
    public partial class ApiClient
    {
        public void InterceptRequest(HttpRequestMessage req)
        {
            // Only for the redirecting upload endpoint
            if (req.Method == HttpMethod.Put &&
                req.RequestUri != null &&
                req.RequestUri.AbsolutePath.EndsWith("/file"))
            {
                // 1) Header-only probe: make server send 307 before we stream the body
                req.Headers.ExpectContinue = true;

                // 2) Match the Content-Type used in the presign (important for signature)
                if (req.Content != null && req.Content.Headers.ContentType == null)
                {
                    req.Content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream"); // or whatever you signed
                }

                // (Optional) If you *know* the content length (seekable stream), set it here.
                // Many S3-compatible endpoints prefer Content-Length over chunked.
                // Unfortunately StreamContent doesn't expose the stream; prefer to set this
                // when you construct the StreamContent at the call site.
            }
        }

        public void InterceptResponse(HttpRequestMessage req, HttpResponseMessage resp)
        {
            // Normally you won't see 307 here because AllowAutoRedirect is true.
            // If you *do*, log it (or throw) to catch handler config issues.
            var code = (int)resp.StatusCode;
            if (code == 307 || code == 308)
            {
                UnityEngine.Debug.LogWarning(
                    $"Redirect not auto-followed for {req.Method} {req.RequestUri} → {resp.Headers.Location}");
            }
        }
    }
}