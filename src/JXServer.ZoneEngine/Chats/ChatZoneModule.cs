using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Chats;

/// <summary>
/// Golden Reference Plug-in Module for multi-channel chat routing (<c>Domain 016</c>).
/// Decoupled from <see cref="MapZoneWorker"/> and dynamically registered via <see cref="ZoneExtensionRegistry"/>.
/// </summary>
public sealed class ChatZoneModule : IZoneModule
{
    public void Configure(IZoneExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var handler = new ChatCommandHandler();
        builder.AddCommandHandler<UpdateChatChannelMaskZoneCommand>(handler);
        builder.AddCommandHandler<SendNearbyTalkZoneCommand>(handler);
        builder.AddCommandHandler<SendStructuredChatZoneCommand>(handler);
        builder.AddCommandHandler<DeliverClusterChatZoneCommand>(handler);
    }
}
