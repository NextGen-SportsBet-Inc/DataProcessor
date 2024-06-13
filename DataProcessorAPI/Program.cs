using Microsoft.EntityFrameworkCore;
using DataProcessorAPI.Data;
using DataProcessorAPI.Repository;
using DataProcessorAPI.Services;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Authorization;
using MassTransit;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DataProcessorAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Load environment variables from .env file
            DotNetEnv.Env.Load(".env");

            Console.WriteLine(Environment.GetEnvironmentVariable("RABBITMQ_HOST"));
            var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
            if (string.IsNullOrEmpty(hostName))
            {
                Console.WriteLine("RABBITMQ_HOST environment variable not set.");
                return;
            }
            
            var builder = WebApplication.CreateBuilder(args);

            //open telemetry
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddOpenTelemetry(options =>
            {
                options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri("" + Environment.GetEnvironmentVariable("OTEL_uri"));
                });
            });

            static void addResource(ResourceBuilder resourceBuilder)
            {
                resourceBuilder.AddService("DataProcessorAPI");
            }

            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(addResource)
                .WithTracing(tracerBuilder => tracerBuilder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri("" + Environment.GetEnvironmentVariable("OTEL_uri"));
                    })
                )
                .WithMetrics(meterBuilder => meterBuilder
                    .AddProcessInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri("" + Environment.GetEnvironmentVariable("OTEL_uri"));
                    })
            );


            // Context
            // Database context injection
            var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
            var dbName = Environment.GetEnvironmentVariable("DB_NAME");
            var dbPassword = Environment.GetEnvironmentVariable("DB_SA_PASSWORD");
            var connectionString = $"Data Source={dbHost},8002;Initial Catalog={dbName};User ID=sa;Password={dbPassword};TrustServerCertificate=True;Encrypt=false";
            builder.Services.AddDbContext<MatchDbContext>(options => options.UseSqlServer(connectionString));
            
            builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration);
            builder.Services
                .AddAuthorization()
                .AddKeycloakAuthorization(options =>
                {
                    options.EnableRolesMapping =
                        RolesClaimTransformationSource.ResourceAccess;
                    options.RolesResource = $"{builder.Configuration["Keycloak:resource"]}";
                })
                .AddAuthorizationBuilder();

            builder.Services.AddScoped<IMatchRepository, MatchRepository>();

            
            // Add services to the container.
            builder.Services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
            
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(new Uri("" + Environment.GetEnvironmentVariable("RabbitMQConnectionURI")), h =>
                    {
                        h.Username("" + Environment.GetEnvironmentVariable("RabbitUser"));
                        h.Password("" + Environment.GetEnvironmentVariable("RabbitPassword"));
                    });
            
                    cfg.ConfigureEndpoints(context);
                });
            
            });
            
            builder.Services.AddScoped<MatchService>();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddHostedService<ProcessorBackgroundService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
