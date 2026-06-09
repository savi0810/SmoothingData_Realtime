using Microsoft.AspNetCore.Diagnostics;
using SmoothingDataApi.Extensions;
using SmoothingDataApi.Hubs;

namespace SmoothingDataApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("FrontendPolicy", policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services.AddSignalRWithDefaults();
            builder.Services.AddStreamingServices();
            builder.Services.AddFilterServices();

            var app = builder.Build();

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                    var isBadRequest = exception is ArgumentException or InvalidOperationException;

                    context.Response.StatusCode = isBadRequest
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status500InternalServerError;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = exception?.Message ?? "Произошла непредвиденная ошибка сервера."
                    });
                });
            });

            app.UseCors("FrontendPolicy");

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.MapHub<PricesHub>("/hubs/prices");

            app.Run();
        }
    }
}
