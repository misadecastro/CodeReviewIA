using CodeReviewIA.Configuration;

namespace CodeReviewIA.Services;

public sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly AzureDevOpsSettings _settings;

    public AuthenticationHandler(AzureDevOpsSettings settings)
    {
        _settings = settings;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("Authorization");
        request.Headers.Add("Authorization", _settings.GetAuthorizationHeader());
        return base.SendAsync(request, cancellationToken);
    }
}
