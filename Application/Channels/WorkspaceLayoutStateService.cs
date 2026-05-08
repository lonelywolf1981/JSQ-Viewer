using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Abstractions;
using JSQViewer.Settings;

namespace JSQViewer.Application.Channels
{
    public sealed class WorkspaceLayoutStateService
    {
        private readonly IWorkspaceLayoutRepository _workspaceLayoutRepository;
        private readonly IOrderRepository _orderRepository;

        public WorkspaceLayoutStateService(IWorkspaceLayoutRepository workspaceLayoutRepository, IOrderRepository orderRepository)
        {
            if (workspaceLayoutRepository == null) throw new ArgumentNullException(nameof(workspaceLayoutRepository));
            if (orderRepository == null) throw new ArgumentNullException(nameof(orderRepository));

            _workspaceLayoutRepository = workspaceLayoutRepository;
            _orderRepository = orderRepository;
        }

        public WorkspaceLayoutState Load(string workspaceKey)
        {
            WorkspaceLayoutState state = _workspaceLayoutRepository.Load(workspaceKey) ?? new WorkspaceLayoutState();
            state.EnsureInitialized();
            return state;
        }

        public WorkspaceLayoutRestoreSelection GetMainRestoreSelection(WorkspaceLayoutState state)
        {
            return CreateRestoreSelection(EnsureState(state).MainSelectedOrderKey, null);
        }

        public WorkspaceLayoutRestoreSelection GetSourceRestoreSelection(WorkspaceLayoutState state, string sourceRoot)
        {
            WorkspaceLayoutState workspaceState = EnsureState(state);
            string normalizedRoot = WorkspaceLayoutState.NormalizeSourceRoot(sourceRoot);

            string selectedOrderKey;
            workspaceState.SourceSelectedOrderKeys.TryGetValue(normalizedRoot, out selectedOrderKey);

            List<string> effectiveOrder;
            workspaceState.SourceEffectiveOrders.TryGetValue(normalizedRoot, out effectiveOrder);

            return CreateRestoreSelection(selectedOrderKey, effectiveOrder);
        }

        public WorkspaceLayoutState SaveMainSelectedOrderKey(string workspaceKey, WorkspaceLayoutState state, string selectedOrderKey)
        {
            WorkspaceLayoutState workspaceState = EnsureState(state);
            workspaceState.MainSelectedOrderKey = NormalizeSelectedOrderKey(selectedOrderKey);
            Save(workspaceKey, workspaceState);
            return workspaceState;
        }

        public WorkspaceLayoutState SaveSourceSelectedOrderKey(string workspaceKey, WorkspaceLayoutState state, string sourceRoot, string selectedOrderKey)
        {
            WorkspaceLayoutState workspaceState = EnsureState(state);
            string normalizedRoot = WorkspaceLayoutState.NormalizeSourceRoot(sourceRoot);
            if (normalizedRoot.Length == 0)
            {
                return workspaceState;
            }

            string normalizedSelectedOrderKey = NormalizeSelectedOrderKey(selectedOrderKey);
            if (string.IsNullOrWhiteSpace(normalizedSelectedOrderKey))
            {
                workspaceState.SourceSelectedOrderKeys.Remove(normalizedRoot);
            }
            else
            {
                workspaceState.SourceSelectedOrderKeys[normalizedRoot] = normalizedSelectedOrderKey;
            }

            Save(workspaceKey, workspaceState);
            return workspaceState;
        }

        public WorkspaceLayoutState SaveSourceEffectiveOrder(string workspaceKey, WorkspaceLayoutState state, string sourceRoot, IEnumerable<string> effectiveOrder)
        {
            WorkspaceLayoutState workspaceState = EnsureState(state);
            string normalizedRoot = WorkspaceLayoutState.NormalizeSourceRoot(sourceRoot);
            if (normalizedRoot.Length == 0)
            {
                return workspaceState;
            }

            List<string> normalizedOrder = NormalizeOrder(effectiveOrder);
            if (normalizedOrder.Count == 0)
            {
                workspaceState.SourceEffectiveOrders.Remove(normalizedRoot);
            }
            else
            {
                workspaceState.SourceEffectiveOrders[normalizedRoot] = normalizedOrder;
            }

            Save(workspaceKey, workspaceState);
            return workspaceState;
        }

        private WorkspaceLayoutRestoreSelection CreateRestoreSelection(string selectedOrderKey, IEnumerable<string> effectiveOrder)
        {
            string normalizedSelectedOrderKey = NormalizeSelectedOrderKey(selectedOrderKey);
            if (!string.IsNullOrWhiteSpace(normalizedSelectedOrderKey))
            {
                ChannelOrderModel namedOrder = _orderRepository.Load(normalizedSelectedOrderKey);
                if (namedOrder != null && namedOrder.order != null && namedOrder.order.Count > 0)
                {
                    return new WorkspaceLayoutRestoreSelection(normalizedSelectedOrderKey, namedOrder.order, true);
                }
            }

            return new WorkspaceLayoutRestoreSelection(normalizedSelectedOrderKey, effectiveOrder, false);
        }

        private void Save(string workspaceKey, WorkspaceLayoutState state)
        {
            if (string.IsNullOrWhiteSpace(workspaceKey))
            {
                return;
            }

            state.EnsureInitialized();
            _workspaceLayoutRepository.Save(workspaceKey, state);
        }

        private static WorkspaceLayoutState EnsureState(WorkspaceLayoutState state)
        {
            WorkspaceLayoutState result = state ?? new WorkspaceLayoutState();
            result.EnsureInitialized();
            return result;
        }

        private static string NormalizeSelectedOrderKey(string selectedOrderKey)
        {
            return string.IsNullOrWhiteSpace(selectedOrderKey) ? null : selectedOrderKey.Trim();
        }

        private static List<string> NormalizeOrder(IEnumerable<string> order)
        {
            return order == null
                ? new List<string>()
                : order
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => code.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }
    }

    public sealed class WorkspaceLayoutRestoreSelection
    {
        public WorkspaceLayoutRestoreSelection(string selectedOrderKey, IEnumerable<string> order, bool hasNamedOrder)
        {
            SelectedOrderKey = string.IsNullOrWhiteSpace(selectedOrderKey) ? null : selectedOrderKey.Trim();
            Order = order == null
                ? new string[0]
                : order
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => code.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            HasNamedOrder = hasNamedOrder;
        }

        public string SelectedOrderKey { get; private set; }

        public IReadOnlyList<string> Order { get; private set; }

        public bool HasNamedOrder { get; private set; }
    }
}
