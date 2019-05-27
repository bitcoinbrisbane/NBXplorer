﻿using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc;
using NBitcoin.JsonConverters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Http.Features;
using NBXplorer.Filters;
using NBXplorer.Logging;
using Microsoft.AspNetCore.Authentication;
using NBXplorer.DB;
using NBXplorer.Authentication;
using NBXplorer.Configuration;
using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO;

namespace NBXplorer
{
	public class Startup
	{
		public Startup(IConfiguration conf, IHostingEnvironment env)
		{
			Configuration = conf;
			_Env = env;
		}
		IHostingEnvironment _Env;
		public IConfiguration Configuration
		{
			get;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddHttpClient();
			services.AddNBXplorer();
			services.ConfigureNBxplorer(Configuration);
			services.AddMvcCore()
				.AddJsonFormatters()
				.AddMvcOptions(o => o.InputFormatters.Add(new NoContentTypeInputFormatter()))
				.AddAuthorization()
				.AddFormatterMappings();
			services.AddAuthentication("Basic")
				.AddNBXplorerAuthentication();

			string certPath = Configuration.GetOrDefault<string>("CertificatePath", null);
			string certPass = Configuration.GetOrDefault<string>("CertificatePassword", null);

			var hasCertPath = !string.IsNullOrEmpty(certPath);
			if (hasCertPath)
			{
				var bindAddress = Configuration.GetOrDefault<IPAddress>("bind", IPAddress.Any);
				int bindPort = Configuration.GetOrDefault<int>("port", 443);
				services.Configure<KestrelServerOptions>(kestrel =>
				{
					if (hasCertPath && !File.Exists(certPath))
					{
						// Note that by design this is a fatal error condition that will cause the process to exit.
						throw new ConfigException($"The https certificate file could not be found at {certPath}.");
					}

					kestrel.Listen(bindAddress, bindPort, l =>
					{
						if (hasCertPath)
						{
							Logs.Configuration.LogInformation($"Using HTTPS with the certificate located in {certPath}.");
							l.UseHttps(certPath, certPass);
						}
						else
						{
							Logs.Configuration.LogInformation($"Using HTTPS with the default certificate");
							l.UseHttps();
						}
					});
				});
			}
		}

		public void Configure(NBXplorerContextFactory dbFactory, IApplicationBuilder app, IServiceProvider prov, IHostingEnvironment env, ILoggerFactory loggerFactory, IServiceProvider serviceProvider,
			CookieRepository cookieRepository, NBXplorer.Configuration.ExplorerConfiguration conf)
		{
			if (!conf.NoCreateDB)
				dbFactory.Migrate();
			cookieRepository.Initialize();
			if(env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			Logs.Configure(loggerFactory);

			app.UseMiddleware<SILOLogAllRequestsMiddleware>();
			app.UseAuthentication();
			app.UseWebSockets();
			//app.UseMiddleware<LogAllRequestsMiddleware>();
			app.UseMvc();
		}
	}
}
