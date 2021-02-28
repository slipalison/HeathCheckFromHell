using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using System;
using System.Threading.Tasks;
using WebApplication2.Controllers;

namespace WebApplication2
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddHealthChecks()
                .AddRabbitMQ(rabbitConnectionString: Configuration.GetConnectionString("RabbitMQ"), tags: new string[] { "RabbitMQ" })
                .AddRedis(Configuration.GetConnectionString("Redis"), tags: new string[] { "Redis" })
                .AddElasticsearch(Configuration.GetConnectionString("ElasticSearch"), tags: new string[] { "elasticsearch" })
                .AddSqlServer(Configuration.GetConnectionString("SqlServer"), tags: new string[] { "SqlServer" })
                .AddCheck(name: "random", () =>
                {
                    return DateTime.UtcNow.Second % 2 == 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
                })
                .AddCheck("self", () => HealthCheckResult.Healthy());

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {

                c.SwaggerDoc("v1",
                    new OpenApiInfo
                    {
                        Title = "Testes",
                        Version = "v1",
                        Description = "Exemplo de API REST criada com o ASP.NET Core 3.0"

                    });
            });


            services
                .AddHealthChecksUI(setup =>
                {
                    setup.SetEvaluationTimeInSeconds(9);
                    setup.MaximumHistoryEntriesPerEndpoint(50);

                    setup.AddHealthCheckEndpoint("endpoint1", "http://localhost/readiness");

                    setup.AddWebhookNotification("webhook1", uri: "http://localhost/api/Notify",
                            payload: "{ \"message\": \"Webhook report for [[LIVENESS]]: [[FAILURE]] - Description: [[DESCRIPTIONS]]\"}",
                            restorePayload: "{ \"message\": \"[[LIVENESS]] is back to life\"}");

                })
                .AddSqlServerStorage(Configuration.GetConnectionString("SqlServer"));

            services.AddMassTransitWithRabbitMq(Configuration);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHealthChecks(new PathString("/liveness"), new HealthCheckOptions
            {
                Predicate = r => r.Name.Contains("self"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            })
            .UseHealthChecks(new PathString("/readiness"), new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            })
            .UseHealthChecks(new PathString("/random-health"), new HealthCheckOptions
            {
                Predicate = r => r.Name.Equals("random", StringComparison.InvariantCultureIgnoreCase),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });


            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Indicadores Econômicos V1");
            });

            app
              .UseRouting()
              .UseEndpoints(config =>
              {
                  config.MapControllers();
                  config.MapHealthChecksUI(op =>
                  {
                      op.UIPath = "/";
                  });

              });
        }
    }



    public static class MassTransitExtension
    {
        public static void AddMassTransitWithRabbitMq(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMassTransit(configure =>
            {
                configure.UsingRabbitMq((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                    cfg.Host(configuration.GetConnectionString("RabbitMQ"));
                    cfg.Publish<Testes>(topology =>
                    {
                        topology.ExchangeType = ExchangeType.Direct;
                    });
                    cfg.UseHealthCheck(context);

                    cfg.ReceiveEndpoint(config =>
                    {
                        config.Consumer<TesteConsumer>();

                        config.Bind<Testes>(queue => {
                            queue.RoutingKey = "ROTA.NA.RUA";
                            queue.ExchangeType = ExchangeType.Direct;
                        });
                    });

                });
            });

            services.AddMassTransitHostedService();
        }
    }


    public class TesteConsumer : IConsumer<Testes>
    {
        public async Task Consume(ConsumeContext<Testes> context)
        {
            var message = context.Message.Message;
            Console.WriteLine($"SMS - {message}");
        }
    }

}
