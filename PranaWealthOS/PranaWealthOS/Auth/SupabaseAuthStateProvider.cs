using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Supabase.Gotrue;

namespace PranaWealthOS.Auth // Keep your internal namespace
{
    public class SupabaseAuthStateProvider : AuthenticationStateProvider
    {
        private readonly Supabase.Client _supabaseClient;

        public SupabaseAuthStateProvider(Supabase.Client supabaseClient)
        {
            _supabaseClient = supabaseClient;

            // FIXED: Using the correct AddStateChangedListener method instead of +=
            _supabaseClient.Auth.AddStateChangedListener((sender, changedState) =>
            {
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            });
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var session = _supabaseClient.Auth.CurrentSession;
            var user = _supabaseClient.Auth.CurrentUser;

            if (session == null || user == null)
            {
                // Not logged in
                var anonymous = new ClaimsIdentity();
                return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(anonymous)));
            }

            // Logged in! Build the user profile for Blazor
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            var identity = new ClaimsIdentity(claims, "SupabaseAuth");
            var principal = new ClaimsPrincipal(identity);

            return Task.FromResult(new AuthenticationState(principal));
        }
    }
}
