using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MyBGList.Models;
using MyBGList.Swagger;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using MyBGList.Constants;
using MyBGList.GraphQL;
using Microsoft.Win32;
using MyBGList.gRPC;
//using Serilog;
//using Serilog.Sinks.MSSqlServer;
//using static System.Net.Mime.MediaTypeNames;

namespace MyBGList
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers(options => {
                options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(
                (x) => $"The value '{x}' is invalid.");
                options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(
                (x) => $"The field {x} must be a number.");
                options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
                (x, y) => $"The value '{x}' is not valid for {y}.");
                options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(
                () => $"A value is required.");
                });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(options =>
            {
                options.ParameterFilter<SortColumnFilter>();
                options.ParameterFilter<SortOrderFilter>();

                // New Swagger security definition
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme 
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "bearer"
                });
                // New Swagger security requirement
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                { 
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                            Array.Empty<string>()
                    }
            });
        });


            builder.Services.AddIdentity<ApiUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 12;
            }).AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                options.DefaultChallengeScheme =
                options.DefaultForbidScheme = 
                options.DefaultScheme =
                options.DefaultSignInScheme = 
                options.DefaultSignOutScheme = 
                JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["JWT:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["JWT:Audience"],
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey
                        (System.Text.Encoding.UTF8.GetBytes(
                            builder.Configuration["JWT:SigningKey"])
                        )
                    };
                });


            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")));

            //Registers the GraphQL server as a service
            builder.Services.AddGraphQLServer() 
            .AddAuthorization() 
            .AddQueryType<Query>() 
            .AddMutationType<Mutation>() 
            .AddProjections() 
            .AddFiltering() 
            .AddSorting();


            //Add the gRPC service
            builder.Services.AddGrpc();




            ////Add the logging Providers
            //builder.Logging
            //.ClearProviders()
            //.AddSimpleConsole()
            //.AddDebug()
            //.AddApplicationInsights(
            //telemetry => telemetry.ConnectionString =
            //builder
            //.Configuration["Azure:ApplicationInsights:ConnectionString"],
            //loggerOptions => { });


            ////Add the Third Party logging Providers (SeriLog)
            //builder.Host.UseSerilog((ctx, lc) =>
            //{
            //    lc.ReadFrom.Configuration(ctx.Configuration);
            //    lc.WriteTo.MSSqlServer(
            //    connectionString:
            //    ctx.Configuration.GetConnectionString("DefaultConnection"),
            //    sinkOptions: new MSSqlServerSinkOptions
            //    {
            //        TableName = "LogEvents",
            //        AutoCreateSqlTable = true
            //    });
            //},
            //writeToProviders: true);


            // Add server-side Caching middelware
            builder.Services.AddResponseCaching(options =>
            {
                options.MaximumBodySize = 32 * 1024 * 1024;  // Sets max response body size to 32 MB
                options.SizeLimit = 50 * 1024 * 1024;        // Sets max middleware size to 50 MB

            });
            //Adds the InMemoryCache service
            builder.Services.AddMemoryCache();

            ////Adds the DistributedSqlServerCache service
            //builder.Services.AddDistributedSqlServerCache(options => 
            //{
            //    options.ConnectionString =
            //    builder.Configuration.GetConnectionString("DefaultConnection"); 
            //    options.SchemaName = "dbo"; 
            //    options.TableName = "AppCache"; 
            //});

            //Adds the DistributedRedisCache service
            builder.Services.AddStackExchangeRedisCache(options => 
            {
                options.Configuration =
                builder.Configuration["Redis:ConnectionString"]; 
            });

            //Allow For Cors To Resourse Sharing
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(conf =>
                  {
                      conf.WithOrigins(builder.Configuration["AllowedOrigins"]);
                      conf.AllowAnyHeader();
                      conf.AllowAnyMethod();
                  });

                options.AddPolicy(name: "AnyOrigin", conf =>
                {
                    conf.AllowAnyOrigin();
                    conf.AllowAnyHeader();
                    conf.AllowAnyMethod();
                });
            }
            );

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
            //    app.UseSwagger();
            //    app.UseSwaggerUI();
            //}
            if (app.Configuration.GetValue<bool>("UseSwagger"))
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (app.Configuration.GetValue<bool>("UseDeveloperExceptionPage")) 
                 app.UseDeveloperExceptionPage(); 
            else
                 app.UseExceptionHandler("/error");



            app.UseHttpsRedirection();

            //The CORS middleware
            app.UseCors();
            app.UseCors("AnyOrigin");


            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGraphQL();
            app.MapGrpcService<GrpcService>();


            //first time we’ve implemented custom middleware—an easy task thanks to the Use extension method.
            // Set the Chaching middleware to not chaching
            app.Use((context, next) => 
{
                context.Response.Headers["cache-control"] =
                "no-cache, no-store";
                return next.Invoke();
            });

            // we use this minimalApi instaed of ErrorController and apply cors for it
            app.MapGet("/error" ,
                [ResponseCache(NoStore = true)] ()=> Results.Problem())
                .RequireCors("AnyOrigins");
            //You Can Also use this for apply cors
            app.MapGet("/error/test", [EnableCors("AnyOrigin")] () => { throw new Exception("test"); });



            //Example to implement COD constrains of REST
            // Servr to this URL ro excute "https://localhost:40443/cod/test."
            app.MapGet("/cod/test",
                [EnableCors("AnyOrigin")]
                [ResponseCache(NoStore = true)] 
                () =>
                Results.Text("<script>" +
                "window.alert('Your client supports JavaScript!" +
                "\\r\\n\\r\\n" +
                $"Server time (UTC): {DateTime.UtcNow.ToString("o")}" +
                "\\r\\n" +
                "Client time (UTC): ' + new Date().toISOString());" +
                "</script>" +
                "<noscript>Your client does not support JavaScript</noscript>",
                "text/html"));
            
            
            
            //set the cache - control header manually instead of relying on the [ResponseCache] attribute.
            app.MapGet("/cache/test/1",
            [EnableCors("AnyOrigin")]
               (HttpContext context) =>
            {
                context.Response.Headers["cache-control"] = 
            "no-cache, no-store"; 
            return Results.Ok();

            });




            //Minimal API caching test method without specifying any caching strategy:
            app.MapGet("/cache/test/2",
                [EnableCors("AnyOrigin")]
                (HttpContext context) =>
                {
                    return Results.Ok();
                });


            app.MapGet("/auth/test/1",
            [EnableCors("AnyOrigin")]
            [ResponseCache(NoStore = true)] () =>
            {
                return Results.Ok("You are authorized!");
            });

            app.MapGet("/auth/test/2",
            [Authorize(Roles = RoleNames.Moderator)]
            [EnableCors("AnyOrigin")]
            [ResponseCache(NoStore = true)] () =>
            {
                return Results.Ok("You are authorized!");
            });

            app.MapGet("/auth/test/3",
            [Authorize(Roles = RoleNames.Administrator)]
            [EnableCors("AnyOrigin")]
            [ResponseCache(NoStore = true)] () =>
            {
                return Results.Ok("You are authorized!");
            });

            app.MapControllers();

            app.Run();
        }
    }
}