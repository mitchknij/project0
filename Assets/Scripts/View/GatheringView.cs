using System.Collections.Generic;
using IdleCloud.Core;
using IdleCloud.Data;
using IdleCloud.Managers;
using UnityEngine;

namespace IdleCloud.View
{
    public sealed class GatheringView : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float harvestRange = 1.0f;
        [SerializeField, Min(1)] private int maxFeedbackTriggersPerTick = 1;
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldMapContext mapContext;
        private PlayerController _player;
        private readonly List<GatheringNodeView> _nodes = new List<GatheringNodeView>();
        private GatheringNodeView _selectedNode;
        private GatheringNodeView _startedNode;
        private GatheringNodeView _displayedNode;

        public void Configure(PlayerController player, IEnumerable<GatheringNodeView> nodes)
        {
            if (_player != null)
            {
                _player.ManualMoveRequested -= HandleManualMove;
                _player.GatheringNodeSelected -= HandleNodeSelected;
            }
            _player = player;
            if (_player != null)
            {
                _player.ManualMoveRequested += HandleManualMove;
                _player.GatheringNodeSelected += HandleNodeSelected;
            }
            _nodes.Clear();
            if (nodes != null) _nodes.AddRange(nodes);
        }

        private void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || mapContext == null)
            {
                Debug.LogWarning("[GatheringView] Assign Player and World Map Context in the Inspector.", this);
                return;
            }
            Configure(player, mapContext.GatheringNodes);
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.ManualMoveRequested -= HandleManualMove;
                _player.GatheringNodeSelected -= HandleNodeSelected;
            }
        }

        private void Update()
        {
            GameManager manager = GameManager.Instance;
            Character character = manager?.GetSelectedCharacter();
            if (character == null || !ActivitySkillMapping.IsHarvest(character.Activity?.Kind ?? ActivityKind.Idle))
            {
                ClearGatheringState();
                return;
            }
            GatheringNodeView node = FindTargetNode(character.Activity.TargetId);
            if (node == null)
            {
                HideProgress();
                return;
            }

            var commands = new List<GatheringCommand>();
            if (_startedNode != node)
            {
                commands.Add(new GatheringCommand { Kind = GatheringCommandKind.Start, TargetEntityId = node.EntityId });
                _startedNode = node;
            }

            float distance = Vector2.Distance(_player.transform.position, node.transform.position);
            ActiveGatheringTickResult result = manager.TickActiveGathering(node.EntityId, node.NodeId,
                new GatheringWorldFacts { TargetAvailable = node.IsAvailable, TargetInRange = distance <= harvestRange }, commands);
            bool tickRejected = result.Simulation == null || !string.IsNullOrWhiteSpace(result.RejectionReason);
            bool movementRequested = false;
            int maxFeedback = Mathf.Max(1, maxFeedbackTriggersPerTick);
            int shakeTriggers = 0;
            int puffTriggers = 0;
            if (result.Simulation?.Events != null)
                foreach (GatheringEvent gatherEvent in result.Simulation.Events)
                {
                    if (gatherEvent.Kind == GatheringEventKind.AttemptResolved && shakeTriggers < maxFeedback)
                    {
                        node.PlayHitShake();
                        shakeTriggers++;
                    }
                    if (gatherEvent.Kind == GatheringEventKind.ResourceGathered && puffTriggers < maxFeedback)
                    {
                        node.PlayCrumblePuff();
                        puffTriggers++;
                    }
                    if (gatherEvent.Kind == GatheringEventKind.MovementRequested)
                    {
                        movementRequested = true;
                        _player.RequestMoveToWorld(node.transform.position);
                    }
                    else if (gatherEvent.Kind == GatheringEventKind.CommandRejected)
                    {
                        tickRejected = true;
                    }
                }

            if (tickRejected || movementRequested)
            {
                if (tickRejected) _startedNode = null;
                HideProgress();
                return;
            }
            if (result.Simulation.ActionIntervalMs <= 0)
            {
                HideProgress();
                return;
            }
            if (_displayedNode != null && _displayedNode != node) _displayedNode.HideProgress();
            _displayedNode = node;
            node.SetProgress(result.Simulation.ActionProgress01);
        }

        private void HandleManualMove(Vector3 _)
        {
            if (_startedNode != null) GameManager.Instance?.Stop();
            ClearGatheringState();
        }

        private void HandleNodeSelected(GatheringNodeView node)
        {
            if (node == null || !_nodes.Contains(node) || !node.IsAvailable) return;
            HideProgress();
            _selectedNode = node;
            _startedNode = null;
            node.Select();
            // GameManager.Assign swallows validation failures by design (store.ts mirror), so
            // post-check the activity actually switched — same pattern as StartActiveCombat.
            Character character = GameManager.Instance?.GetSelectedCharacter();
            bool assigned = character != null &&
                ActivitySkillMapping.IsHarvest(character.Activity?.Kind ?? ActivityKind.Idle) &&
                character.Activity.TargetId == node.NodeId;
            if (!assigned)
            {
                RuntimeContent.Nodes.TryGetValue(node.NodeId, out ResourceNodeDef definition);
                Debug.LogWarning(
                    $"[GatheringView] Gathering '{node.NodeId}' was not started — assignment rejected " +
                    $"(node map '{definition?.MapId}' vs character map '{character?.MapId}', or level requirement not met).",
                    node);
                _selectedNode = null;
            }
        }

        private GatheringNodeView FindTargetNode(string nodeId)
        {
            if (_selectedNode != null && _selectedNode.IsAvailable && _selectedNode.NodeId == nodeId)
                return _selectedNode;
            return _nodes.Find(candidate => candidate != null && candidate.NodeId == nodeId && candidate.IsAvailable);
        }

        private void HideProgress()
        {
            if (_displayedNode != null) _displayedNode.HideProgress();
            _displayedNode = null;
        }

        private void ClearGatheringState()
        {
            HideProgress();
            _selectedNode = null;
            _startedNode = null;
        }
    }
}
