using Microsoft.AspNetCore.HttpOverrides;
using System.Text;
using YaGpt;


Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Const.Init();

await Test.GO();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy
                    .SetIsOriginAllowed(origin => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
        });
});
builder.Services.AddCertificateForwarding(options => options.CertificateHeader = "X-ARR-ClientCert");
builder.Services.Configure<ForwardedHeadersOptions>(options => { options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto; });
builder.Services.AddScoped<HttpHelper.IWebHandlerClient, HttpHelper.YaClient>();
builder.Services.AddScoped<SQL.ITConnection, SQL.TConnection>();
builder.Services.AddScoped<YaGpt.Sql.Templates.LogItem>();

var app = builder.Build();

app
    .UseCors()
    .UseMiddleware<MiddlewareAuth>()
    .UseMiddleware<MiddlewareHandler>();



app.Run();


