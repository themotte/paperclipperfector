using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;

namespace PaperclipPerfector
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        bool isAuthorized = false;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "[unknown username]"),
            }, "Fake authentication type");

            if (isAuthorized)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "moderator"));
            }

            var user = new ClaimsPrincipal(identity);

            var auth = new AuthenticationState(user);

            return Task.FromResult(auth);
        }

        public void SetAuthorized(bool authorized)
        {
            if (isAuthorized == authorized)
            {
                return;
            }

            isAuthorized = authorized;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
