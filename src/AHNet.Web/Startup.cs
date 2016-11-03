﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AHNet.Web.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using AHNet.Web.Core;
using AHNet.Web.Core.AutoMapper;
using AutoMapper;
using Sakura.AspNetCore.Mvc;
using System;
using AHNet.Web.Infrastructure.Utilities;
using AHNet.Web.Core.Entities;

namespace AHNet.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            CurrentEnvironment = env;

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
            .AddJsonFile("conf/appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"conf/appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

            builder.AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                builder.AddUserSecrets();
            }

            Configuration = builder.Build();

            InitializeMapperConfiguration();
        }

        private void InitializeMapperConfiguration()
        {
            _mapperConfiguration = new MapperConfiguration(cfg => { cfg.AddProfile(new AutoMapperProfileConfiguration()); });
        }

        public IConfigurationRoot Configuration { get; }
        public IHostingEnvironment CurrentEnvironment { get; }

        private MapperConfiguration _mapperConfiguration { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDatabase(Configuration);

            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<AHNetDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvcWithFeatureRouting();

            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
            });

            services.AddSingleton(_ => Configuration);

            services.AddTransient<SeedData>();

            services.AddScoped<BlogPostRepository>();

            services.AddScoped<ContentTagRepository>();

            services.AddSingleton<IMapper>(sp => _mapperConfiguration.CreateMapper());

            services.AddBootstrapPagerGenerator(options =>
            {
                options.ConfigureDefault();
                options.HideOnSinglePage = true;
                options.PagerItemsForEndings = 0;
                options.ExpandPageItemsForCurrentPage = 2;
            });

        }

        public async void Configure(IApplicationBuilder app,
                        IHostingEnvironment env,
                        ILoggerFactory loggerFactory,
                        SeedData seedData)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));

            if (env.IsDevelopment())
            {
                loggerFactory.AddDebug(LogLevel.Information);
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseBrowserLink();
            }
            else
            {
                loggerFactory.AddConsole(LogLevel.Error);
                app.UseExceptionHandler("/error/{0}");
            }

            app.UseStatusCodePagesWithReExecute("/error/{0}");

            app.UseStaticFiles();

            app.UseIdentity();

            app.UseCookieAuthentication(GetCookieAuthenticationConfiguration());

            app.UseMvc(ConfigureRoutes);

            if (env.IsDevelopment())
            {
                await seedData.DevelopInitializeAsync();
            }
            else
            {
                await seedData.InitializeAsync();
            }

        }

        private static CookieAuthenticationOptions GetCookieAuthenticationConfiguration()
        {
            return new CookieAuthenticationOptions()
            {
                AuthenticationScheme = "Cookie",
                LoginPath = new PathString("/account/login"),
                AutomaticAuthenticate = true,
                AutomaticChallenge = true
            };
        }

        private static void ConfigureRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute(
                name: "default",
                template: "{controller=home}/{action=index}/{id?}");
        }
    }

    public static class StartupExtensionMethods
    {
        public static void AddMvcWithFeatureRouting(this IServiceCollection services)
        {
            services.AddMvc(options => options.Conventions.Add(new FeatureConvention()))
                .AddTagHelpersAsServices()
                .AddRazorOptions(options =>
                {
                    options.ViewLocationFormats.Clear();
                    options.ViewLocationFormats.Add("/Features/{3}/{1}/{0}.cshtml");
                    options.ViewLocationFormats.Add("/Features/{3}/{0}.cshtml");
                    options.ViewLocationFormats.Add("/Features/Shared/{0}.cshtml");
                    options.ViewLocationExpanders.Add(new FeatureViewLocationExpander());
                });
        }

        public static void AddDatabase(this IServiceCollection services, IConfigurationRoot configuration)
        {
            switch (configuration.GetValue<string>("AHNET_DBTYPE").ToLower())
            {
                case "postgres":
                case "postgresql":
                case "pgsql":
                    var connectionString = new PostgreSqlConnectionString()
                    {
                        DatabaseHost = configuration.GetValue<string>("AHNET_DBHOST") ?? string.Empty,
                        DatabaseName = configuration.GetValue<string>("AHNET_DBNAME") ?? string.Empty,
                        DatabaseOwner = configuration.GetValue<string>("AHNET_DBOWNER") ?? string.Empty,
                        DatabasePassword = configuration.GetValue<string>("AHNET_DBPASSWORD") ?? string.Empty,
                        DatabasePort = configuration.GetValue<string>("AHNET_DBPORT") ?? string.Empty,
                        DatabasePooling = configuration.GetValue<string>("AHNET_DBPOOLING") ?? string.Empty
                    }.ToString();
                    services.AddDbContext<AHNetDbContext>(options => 
                        options.UseNpgsql(connectionString));
                    break;
                case "inmemory":
                case "inmem":
                case "in-memory":
                    services.AddDbContext<AHNetDbContext>(options =>
                        options.UseInMemoryDatabase());
                    break;
                default:
                    throw new Exception("No database specified.");
            }
        }
    }
}
