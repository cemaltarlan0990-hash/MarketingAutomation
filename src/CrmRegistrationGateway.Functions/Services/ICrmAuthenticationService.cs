namespace CrmRegistrationGateway.Services;

public interface ICrmAuthenticationService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
