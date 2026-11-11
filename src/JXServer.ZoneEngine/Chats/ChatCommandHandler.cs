using System.Text;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Chats;

/// <summary>
/// Domain command handler executing all chat channel operations (<c>Domain 016</c>):
/// channel mask updates, <c>3x3</c> AOI spatial speech broadcasts, structured channel messaging,
/// and cluster-routed broadcasts.
/// </summary>
public sealed class ChatCommandHandler :
    IZoneCommandHandler<UpdateChatChannelMaskZoneCommand>,
    IZoneCommandHandler<SendNearbyTalkZoneCommand>,
    IZoneCommandHandler<SendStructuredChatZoneCommand>,
    IZoneCommandHandler<DeliverClusterChatZoneCommand>
{
    public ValueTask HandleAsync(
        UpdateChatChannelMaskZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        if (context.TryGetPlayer(command.CharacterId, out PlayerZoneState? state) &&
            state.LeaseEpoch == command.LeaseEpoch)
        {
            state.SubscribedChatChannelMask = command.SubscribedChannelMask;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        SendNearbyTalkZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.TryGetPlayer(command.SenderCharacterId, out PlayerZoneState? sender) ||
            sender.LeaseEpoch != command.LeaseEpoch ||
            string.IsNullOrWhiteSpace(command.MessageUtf8))
        {
            return ValueTask.CompletedTask;
        }

        SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
        int regionCount = context.CollectAoiRegions(sender.RegionX, sender.RegionY, buffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = buffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendPlayerTalkBroadcast(sender.RuntimeNpcId, command.MessageUtf8);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        SendStructuredChatZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.TryGetPlayer(command.SenderCharacterId, out PlayerZoneState? sender) ||
            sender.LeaseEpoch != command.LeaseEpoch ||
            string.IsNullOrWhiteSpace(command.MessageUtf8))
        {
            return ValueTask.CompletedTask;
        }

        if (command.Channel is ChatChannelKind.Nearby or ChatChannelKind.Screen)
        {
            uint channelBit = 1u << (int)command.Channel;
            SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
            int regionCount = context.CollectAoiRegions(sender.RegionX, sender.RegionY, buffer);
            for (int i = 0; i < regionCount; i++)
            {
                List<PlayerZoneState> players = buffer[i].Players;
                for (int pIdx = 0; pIdx < players.Count; pIdx++)
                {
                    PlayerZoneState recipient = players[pIdx];
                    if ((recipient.SubscribedChatChannelMask & channelBit) != 0)
                    {
                        recipient.OutboundSink.SendChatMessage(sender.RuntimeNpcId, sender.RoleNameUnicode, command.MessageUtf8);
                    }
                }
            }

            return ValueTask.CompletedTask;
        }

        var clusterChat = new DeliverClusterChatZoneCommand(
            SenderNpcId: sender.RuntimeNpcId,
            SenderNameUtf8: sender.RoleNameUnicode,
            Channel: command.Channel,
            TargetNpcId: command.TargetNpcId,
            TargetRoleNameUtf8: command.TargetOrPrefixUtf8 ?? string.Empty,
            SenderFactionId: sender.CurrentFactionId,
            MessageUtf8: command.MessageUtf8);

        if (context.Assets.ClusterRouter is not null)
        {
            context.Assets.ClusterRouter.RouteClusterChat(clusterChat);
        }
        else
        {
            DeliverClusterChat(clusterChat, context);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        DeliverClusterChatZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        DeliverClusterChat(command, context);
        return ValueTask.CompletedTask;
    }

    private static void DeliverClusterChat(DeliverClusterChatZoneCommand cmd, ZoneExecutionContext context)
    {
        if (context.PlayersByCharacterId.Count == 0 || string.IsNullOrWhiteSpace(cmd.MessageUtf8))
        {
            return;
        }

        if (cmd.Channel == ChatChannelKind.System)
        {
            foreach (PlayerZoneState recipient in context.PlayersByCharacterId.Values)
            {
                recipient.OutboundSink.SendSystemMessage(cmd.MessageUtf8);
            }

            return;
        }

        uint channelBit = 1u << (int)cmd.Channel;
        string normalizedTargetName = NormalizeRoleName(cmd.TargetRoleNameUtf8);
        string normalizedSenderName = NormalizeRoleName(cmd.SenderNameUtf8);

        foreach (PlayerZoneState recipient in context.PlayersByCharacterId.Values)
        {
            if ((recipient.SubscribedChatChannelMask & channelBit) == 0)
            {
                continue;
            }

            bool deliver = cmd.Channel switch
            {
                ChatChannelKind.World or ChatChannelKind.All => true,
                ChatChannelKind.PrivateWhisper =>
                    (cmd.TargetNpcId != 0 && recipient.RuntimeNpcId == cmd.TargetNpcId) ||
                    (!string.IsNullOrEmpty(normalizedTargetName) &&
                     string.Equals(NormalizeRoleName(recipient.RoleNameUnicode), normalizedTargetName, StringComparison.Ordinal)) ||
                    string.Equals(NormalizeRoleName(recipient.RoleNameUnicode), normalizedSenderName, StringComparison.Ordinal),
                ChatChannelKind.Faction =>
                    cmd.SenderFactionId != CharacterConsts.NoFactionId &&
                    recipient.CurrentFactionId == cmd.SenderFactionId,
                ChatChannelKind.Team or ChatChannelKind.Tong => true,
                _ => false,
            };

            if (deliver)
            {
                recipient.OutboundSink.SendChatMessage(cmd.SenderNpcId, cmd.SenderNameUtf8, cmd.MessageUtf8);
            }
        }
    }

    private static string NormalizeRoleName(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC);
}
