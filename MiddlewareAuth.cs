using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static YaGpt.ErrorsHelper;



namespace YaGpt
{
    public class MiddlewareAuth
    {
        private RequestDelegate next;
        public MiddlewareAuth(RequestDelegate next)
        {
            this.next = next;
        }

        const string ISSUER = "XXX"; // издатель токена
        const string AUDIENCE = "YYY"; // потребитель токена
        long currentExt => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        public class AuthOptions
        {
            public DateTime EXPIRES { get; private set; }
            private string KEY { get; set; }
            public SymmetricSecurityKey GetSymmetricSecurityKey => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));

            public AuthOptions(string key)
            {
                KEY = key;
                EXPIRES = DateTime.UtcNow.Add(TimeSpan.FromMinutes(15));
            }
        }

        public async Task InvokeAsync(HttpContext context)        
        {            
            try
            {
                if (context.Request.Method != HttpMethod.Post.ToString())
                    throw new Exception("Request error");               

                switch (context.Request.Path.Value.ToLower())
                {
                    case "/get-token":

                        string body;
                        using (var reader = new StreamReader(context.Request.BodyReader.AsStream()))
                            body = await reader.ReadToEndAsync();

                        var authData = JsonSerializer.Deserialize<Model.AuthData>(body);
                        if (!Const.Config.allow_tokens.Contains(authData.token))
                            throw new HttpSimpleException("Invalid auth key", HttpStatusCode.Forbidden);

                        var authOptions = new AuthOptions(authData.token);

                        var newJwt = new JwtSecurityToken(
                                issuer: ISSUER,
                                audience: AUDIENCE,
                                claims: new List<Claim> { new Claim(ClaimTypes.Authentication, authData.token) },
                                expires: authOptions.EXPIRES,
                                signingCredentials: new SigningCredentials(authOptions.GetSymmetricSecurityKey, SecurityAlgorithms.HmacSha256));

                        var newToken = new JwtSecurityTokenHandler().WriteToken(newJwt);

                        await context.Response.WriteAsync(new Model.AuthData(newToken, authOptions.EXPIRES).ToStr());
                        
                        break;

                    default:


                        var auth = context.Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;
                        var jwt = auth.Split(' ').OrderByDescending(m => m?.Length ?? 0).FirstOrDefault();
                        var jwtData = new JwtSecurityTokenHandler().ReadJwtToken(jwt);                       
                        
                        if (jwtData.Issuer != ISSUER)
                            throw new HttpSimpleException("Invalid auth ISSUER", HttpStatusCode.Forbidden);
                        
                        if (!jwtData.Audiences.Contains(AUDIENCE))
                            throw new HttpSimpleException("Invalid auth AUDIENCE", HttpStatusCode.Forbidden);
                        
                        var jwtKey = jwtData.Claims.FirstOrDefault(m => m.Type == ClaimTypes.Authentication)?.Value;
                        if (!Const.Config.allow_tokens.Contains(jwtKey))
                            throw new HttpSimpleException("Invalid auth KEY", HttpStatusCode.Forbidden);

                        var jwtExpired = jwtData.Claims.FirstOrDefault(m => m.Type == "exp")?.Value;
                        if (!long.TryParse(jwtExpired, out long jwtExpiredDt) || jwtExpiredDt < currentExt)
                            throw new HttpSimpleException("Invalid auth EXPIRED", HttpStatusCode.Forbidden);

                        await next.Invoke(context);
                        break;
                }
                
            }
            catch (HttpSimpleException e)
            {
                await context.WriteResult(e.Status, e);
            }  
            catch (Exception e)
            {
                await context.WriteResult(HttpStatusCode.BadRequest, e);
            }

            
        }
    }
}
