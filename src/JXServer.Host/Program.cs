using JXServer.Adapter.LegacyJX1;
using JXServer.Application;
using JXServer.GameAssets;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Infrastructure;
using JXServer.ZoneEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddJXServerInfrastructure(builder.Configuration)
    .AddJXServerGameAssets(builder.Configuration)
    .AddJXServerApplication(builder.Configuration)
    .AddJXServerZoneEngine(builder.Configuration)
    .AddJXServerLegacyJX1Adapter(builder.Configuration);

using IHost host = builder.Build();

// Validate critical static assets on startup before opening client TCP ports (FR-012)
_ = host.Services.GetRequiredService<IMapCatalog>();
_ = host.Services.GetRequiredService<IPlayerTemplateCatalog>();

await host.RunAsync();
