
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tui
{
    /// <summary>
    /// Facade for TigerCli's semi-interactive prompts, message boxes, folder picker, custom dialog
    /// hosting, and activity/progress dialogs.
    /// </summary>
    /// <remarks>
    /// Default overloads run on the standard console-backed semi-interactive shell. Shell-injected
    /// overloads take an <see cref="ICliAppShell"/> first so tests and custom hosts can provide their
    /// own terminal boundary, culture, viewport, interaction mode, timeout behavior, and cancellation
    /// policy.
    /// <para>
    /// Most prompt families have simple adapters (for example <c>SelectIndexAsync</c> or
    /// <c>InputAsync</c>) and rich <c>*ResultAsync</c> siblings. The simple adapters collapse cancel,
    /// timeout, token cancellation, and interaction-not-allowed outcomes to <c>null</c> or another
    /// simple return shape. The rich variants return <see cref="TigerTuiResult{T}"/> so callers can
    /// inspect the exact <see cref="DialogResultKind"/>.
    /// </para>
    /// <para>
    /// Process/system cancellation (<see cref="DialogResultKind.SystemCancel"/>) is never silently
    /// collapsed by the simple adapters: they throw <see cref="TigerCliSystemCancellationException"/>.
    /// Rich result APIs surface <see cref="DialogResultKind.SystemCancel"/> as a normal result kind.
    /// Activity APIs return <see cref="ActivityResult{T}"/> instead of the prompt result shape and run
    /// headlessly in non-interactive mode because an activity is work-with-presentation, not a prompt.
    /// </para>
    /// </remarks>
    public static partial class TigerTui
    {
        /// <inheritdoc cref="SelectAsync{T}(ICliAppShell, string, T?, TimeSpan?, CancellationToken)"/>
        public static async Task<T?> SelectAsync<T>(
            string title,
            T? preselect = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
            where T : struct, Enum
        {
            return await SelectAsync(InlineShell.Instance, title, preselect, timeout, ct).ConfigureAwait(false);
        }

        /// <summary>Selects one value from the members of an enum.</summary>
        /// <typeparam name="T">The enum type to select from.</typeparam>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="preselect">The value selected initially, or <c>null</c> to select the first value.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected enum value, or <c>null</c> when no value is selected. Interactive and
        /// semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <c>null</c>.
        /// </returns>
        public static async Task<T?> SelectAsync<T>(
            ICliAppShell shell,
            string title,
            T? preselect = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
            where T : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(shell);

            var values = Enum.GetValues<T>();

            // labels shown to the user
            var labels = new List<string>(values.Length);
            for (int i = 0; i < values.Length; i++)
                labels.Add(values[i]!.ToString());

            // preselect index
            int? idx = preselect.HasValue
                ? Array.IndexOf(values, preselect.Value)
                : (int?)null;

            // run the core picker
            var picked = await SelectIndexAsync(shell, title, labels, idx, timeout: timeout, ct: ct).ConfigureAwait(false);

            // map index -> enum value
            return picked is int ix ? values[ix] : (T?)null;
        }

        /// <inheritdoc cref="SelectAsync(ICliAppShell, string, IReadOnlyList{string}, string?, SelectOrder, IComparer{string}?, CliFormattingMode, TimeSpan?, CancellationToken)"/>
        public static async Task<string?> SelectAsync(
            string title,
            IReadOnlyList<string> items,
            string? preselect = null,
            SelectOrder order = SelectOrder.Insertion,
            IComparer<string>? customOrder = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return await SelectAsync(InlineShell.Instance, title, items, preselect, order, customOrder, itemsFormattingMode, timeout, ct).ConfigureAwait(false);
        }

        /// <summary>Selects one string from a list.</summary>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="items">The values available for selection.</param>
        /// <param name="preselect">The value selected initially, or <c>null</c> to select the first value.</param>
        /// <param name="order">The order in which to display the values.</param>
        /// <param name="customOrder">The comparer used when <paramref name="order"/> is <see cref="SelectOrder.Custom"/>.</param>
        /// <param name="itemsFormattingMode">How item labels are formatted.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected value, or <c>null</c> when no value is selected. Interactive and
        /// semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <c>null</c>.
        /// </returns>
        public static async Task<string?> SelectAsync(
            ICliAppShell shell,
            string title,
            IReadOnlyList<string> items,
            string? preselect = null,
            SelectOrder order = SelectOrder.Insertion,
            IComparer<string>? customOrder = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);

            // order view (don’t mutate original)
            IReadOnlyList<string> view = order switch
            {
                SelectOrder.Insertion => items,
                SelectOrder.ByLabel => items.OrderBy(s => s, StringComparer.CurrentCulture).ToList(),
                SelectOrder.Custom => customOrder is null ? items : items.OrderBy(s => s, customOrder).ToList(),
                _ => items
            };

            // preselect index in the *view*
            int? idx = null;
            if (preselect is not null)
            {
                for (int i = 0; i < view.Count; i++)
                    if (string.Equals(view[i], preselect, StringComparison.Ordinal)) { idx = i; break; }
            }

            var picked = await SelectIndexAsync(shell, title, view, idx, itemsFormattingMode, timeout, ct).ConfigureAwait(false);
            return picked is int ix ? view[ix] : null;
        }

        /// <inheritdoc cref="SelectAsync{TKey}(ICliAppShell, string, IReadOnlyList{OptionItem{TKey}}, int?, SelectOrder, IComparer{OptionItem{TKey}}?, IEqualityComparer{TKey}?, TimeSpan?, CancellationToken)"/>
        public static async Task<TKey?> SelectAsync<TKey>(
            string title,
            IReadOnlyList<OptionItem<TKey>> items,
            int? preselectIndex = null,
            SelectOrder order = SelectOrder.Insertion,
            IComparer<OptionItem<TKey>>? customOrder = null,
            IEqualityComparer<TKey>? keyComparer = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)            
        {
            return await SelectAsync(InlineShell.Instance, title, items, preselectIndex, order, customOrder, keyComparer, timeout, ct).ConfigureAwait(false);
        }

        /// <summary>Selects the key of one option from a list.</summary>
        /// <typeparam name="TKey">The option key type.</typeparam>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="items">The keyed options available for selection.</param>
        /// <param name="preselectIndex">The initially selected option index, or <c>null</c> for the first option.</param>
        /// <param name="order">The order in which to display the options.</param>
        /// <param name="customOrder">The comparer used when <paramref name="order"/> is <see cref="SelectOrder.Custom"/>.</param>
        /// <param name="keyComparer">The comparer used for option keys.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected key, or the default value when no option is selected. Interactive and
        /// semi-interactive shells render the prompt; non-interactive shells do not read input.
        /// </returns>
        public static async Task<TKey?> SelectAsync<TKey>(
            ICliAppShell shell,
            string title,
            IReadOnlyList<OptionItem<TKey>> items,
            int? preselectIndex = null,
            SelectOrder order = SelectOrder.Insertion,
            IComparer<OptionItem<TKey>>? customOrder = null,
            IEqualityComparer<TKey>? keyComparer = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);

            // order view
            List<OptionItem<TKey>> view = order switch
            {
                SelectOrder.Insertion => items.ToList(),
                SelectOrder.ByKey => items.OrderBy(i => i.Key, Comparer<TKey>.Default).ToList(),
                SelectOrder.ByLabel => items.OrderBy(i => i.Label, StringComparer.CurrentCulture).ToList(),
                SelectOrder.Custom => customOrder is null ? items.ToList() : items.OrderBy(i => i, customOrder).ToList(),
                _ => items.ToList()
            };

            // labels for the core
            var labels = view.Select(i => i.Label).ToList();

            var picked = await SelectIndexAsync(shell, title, labels, preselectIndex, timeout: timeout, ct: ct).ConfigureAwait(false);
            return picked is int i ? view[i].Key : default;
        }

        /// <inheritdoc cref="SelectIndexAsync(ICliAppShell, string, IReadOnlyList{string?}, int?, CliFormattingMode, TimeSpan?, CancellationToken)"/>
        public static async Task<int?> SelectIndexAsync(
             string title,
             IReadOnlyList<string?> labels,
             int? preselectIndex = null,
             CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
             TimeSpan? timeout = default,
             CancellationToken ct = default)
        {
            return await SelectIndexAsync(InlineShell.Instance, title, labels, preselectIndex, itemsFormattingMode, timeout, ct).ConfigureAwait(false);
        }

        /// <summary>Selects the index of one label.</summary>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="labels">
        /// The labels to display. A <c>null</c> label represents a selectable no-selection row.
        /// </param>
        /// <param name="preselectIndex">The initially selected index, or <c>null</c> for the first label.</param>
        /// <param name="itemsFormattingMode">How labels are formatted.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected index, or <c>null</c> when no label is selected. Interactive and
        /// semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <c>null</c>.
        /// </returns>
        public static async Task<int?> SelectIndexAsync(
             ICliAppShell shell,
             string title,
             IReadOnlyList<string?> labels,
             int? preselectIndex = null,
             CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
             TimeSpan? timeout = default,
             CancellationToken ct = default)
        {
            var result = await SelectIndexResultAsync(shell, title, labels, preselectIndex, itemsFormattingMode, timeout, ct).ConfigureAwait(false);
            ThrowIfSystemCancel(result.ResultKind);
            return result.IsOk ? result.Value : null;
        }

        /// <inheritdoc cref="SelectIndexResultAsync(ICliAppShell, string, IReadOnlyList{string?}, int?, CliFormattingMode, TimeSpan?, CancellationToken)"/>
        public static Task<TigerTuiResult<int>> SelectIndexResultAsync(
             string title,
             IReadOnlyList<string?> labels,
             int? preselectIndex = null,
             CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
             TimeSpan? timeout = default,
             CancellationToken ct = default)
        {
            return SelectIndexResultAsync(InlineShell.Instance, title, labels, preselectIndex, itemsFormattingMode, timeout, ct);
        }

        /// <summary>Selects the index of one label and preserves the exact dialog result.</summary>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="labels">The labels to display; a <c>null</c> label is a selectable no-selection row.</param>
        /// <param name="preselectIndex">The initially selected index, or <c>null</c> for the first label.</param>
        /// <param name="itemsFormattingMode">How labels are formatted.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// A result containing the selected index and exact outcome. Interactive and semi-interactive
        /// shells render the prompt; non-interactive shells do not read input and return
        /// <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static async Task<TigerTuiResult<int>> SelectIndexResultAsync(
             ICliAppShell shell,
             string title,
             IReadOnlyList<string?> labels,
             int? preselectIndex = null,
             CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
             TimeSpan? timeout = default,
             CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);

            var select = new InlineSelect(shell, labels, preselectIndex, itemsFormattingMode);
            var dialog = new InlineDialog(shell, title, select);
            var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
            return dr.Kind == DialogResultKind.Ok && dr.Payload is int ix
                ? TigerTuiResult<int>.Ok(ix)
                : TigerTuiResult<int>.FromKind(dr.Kind);
        }

        /// <inheritdoc cref="MultiSelectIndexesAsync(ICliAppShell, string, IReadOnlyList{string}, IReadOnlyCollection{int}?, CliFormattingMode, TimeSpan?, CancellationToken)"/>
        public static Task<int[]?> MultiSelectIndexesAsync(
            string title,
            IReadOnlyList<string> labels,
            IReadOnlyCollection<int>? preselectedIndexes = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MultiSelectIndexesAsync(InlineShell.Instance, title, labels, preselectedIndexes, itemsFormattingMode, timeout, ct);
        }

        /// <summary>Selects zero or more label indexes.</summary>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="labels">The labels available for selection.</param>
        /// <param name="preselectedIndexes">The indexes selected initially.</param>
        /// <param name="itemsFormattingMode">How labels are formatted.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected indexes, or <c>null</c> when the prompt does not complete successfully.
        /// Interactive and semi-interactive shells render the prompt; non-interactive shells do not
        /// read input and return <c>null</c>.
        /// </returns>
        public static async Task<int[]?> MultiSelectIndexesAsync(
            ICliAppShell shell,
            string title,
            IReadOnlyList<string> labels,
            IReadOnlyCollection<int>? preselectedIndexes = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            var result = await MultiSelectIndexesResultAsync(shell, title, labels, preselectedIndexes, itemsFormattingMode, timeout, ct).ConfigureAwait(false);
            ThrowIfSystemCancel(result.ResultKind);
            return result.IsOk ? result.Value : null;
        }

        /// <inheritdoc cref="MultiSelectIndexesResultAsync(ICliAppShell, string, IReadOnlyList{string}, IReadOnlyCollection{int}?, CliFormattingMode, TimeSpan?, CancellationToken)"/>
        public static Task<TigerTuiResult<int[]>> MultiSelectIndexesResultAsync(
            string title,
            IReadOnlyList<string> labels,
            IReadOnlyCollection<int>? preselectedIndexes = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MultiSelectIndexesResultAsync(InlineShell.Instance, title, labels, preselectedIndexes, itemsFormattingMode, timeout, ct);
        }

        /// <summary>Selects zero or more label indexes and preserves the exact dialog result.</summary>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="labels">The labels available for selection.</param>
        /// <param name="preselectedIndexes">The indexes selected initially.</param>
        /// <param name="itemsFormattingMode">How labels are formatted.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// A result containing the selected indexes and exact outcome. Interactive and
        /// semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static async Task<TigerTuiResult<int[]>> MultiSelectIndexesResultAsync(
            ICliAppShell shell,
            string title,
            IReadOnlyList<string> labels,
            IReadOnlyCollection<int>? preselectedIndexes = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);
            ArgumentNullException.ThrowIfNull(labels);

            var select = new InlineMultiSelect(shell, labels, preselectedIndexes, itemsFormattingMode);
            var dialog = new InlineDialog(shell, title, select);
            var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
            return dr.Kind == DialogResultKind.Ok && dr.Payload is int[] indexes
                ? TigerTuiResult<int[]>.Ok(indexes)
                : TigerTuiResult<int[]>.FromKind(dr.Kind);
        }

        /// <inheritdoc cref="MultiSelectAsync{T}(ICliAppShell, string, IReadOnlyList{T}, Func{T, string}, IReadOnlyCollection{T}?, IEqualityComparer{T}?, CliFormattingMode, TimeSpan?, CancellationToken)"/>
        public static Task<IReadOnlyList<T>?> MultiSelectAsync<T>(
            string title,
            IReadOnlyList<T> items,
            Func<T, string> labelSelector,
            IReadOnlyCollection<T>? preselected = null,
            IEqualityComparer<T>? comparer = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MultiSelectAsync(InlineShell.Instance, title, items, labelSelector, preselected, comparer, itemsFormattingMode, timeout, ct);
        }

        /// <summary>Selects zero or more items using caller-provided labels.</summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="items">The items available for selection.</param>
        /// <param name="labelSelector">A function that produces the displayed label for each item.</param>
        /// <param name="preselected">The items selected initially.</param>
        /// <param name="comparer">The comparer used to match preselected items.</param>
        /// <param name="itemsFormattingMode">How item labels are formatted.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected items, or <c>null</c> when the prompt does not complete successfully.
        /// Interactive and semi-interactive shells render the prompt; non-interactive shells do not
        /// read input and return <c>null</c>.
        /// </returns>
        public static async Task<IReadOnlyList<T>?> MultiSelectAsync<T>(
            ICliAppShell shell,
            string title,
            IReadOnlyList<T> items,
            Func<T, string> labelSelector,
            IReadOnlyCollection<T>? preselected = null,
            IEqualityComparer<T>? comparer = null,
            CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(labelSelector);

            var labels = items.Select(labelSelector).ToList();
            int[]? indexes = await MultiSelectIndexesAsync(
                shell,
                title,
                labels,
                ResolvePreselectedIndexes(items, preselected, comparer),
                itemsFormattingMode,
                timeout,
                ct).ConfigureAwait(false);

            if (indexes is null)
                return null;

            var selected = new List<T>(indexes.Length);
            foreach (int index in indexes)
                selected.Add(items[index]);

            return selected;
        }

        /// <inheritdoc cref="MultiSelectFlagsAsync{TEnum}(ICliAppShell, string, TEnum, Func{TEnum, string}?, TimeSpan?, CancellationToken)"/>
        public static Task<TEnum?> MultiSelectFlagsAsync<TEnum>(
            string title,
            TEnum selected = default,
            Func<TEnum, string>? labelSelector = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
            where TEnum : struct, Enum
        {
            return MultiSelectFlagsAsync(InlineShell.Instance, title, selected, labelSelector, timeout, ct);
        }

        /// <summary>Selects zero or more single-bit members of a flags enum.</summary>
        /// <typeparam name="TEnum">The flags enum type.</typeparam>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="selected">The flags selected initially.</param>
        /// <param name="labelSelector">An optional function that produces each flag label.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// The selected flags, or <c>null</c> when the prompt does not complete successfully.
        /// Interactive and semi-interactive shells render the prompt; non-interactive shells do not
        /// read input and return <c>null</c>.
        /// </returns>
        public static async Task<TEnum?> MultiSelectFlagsAsync<TEnum>(
            ICliAppShell shell,
            string title,
            TEnum selected = default,
            Func<TEnum, string>? labelSelector = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
            where TEnum : struct, Enum
        {
            var result = await MultiSelectFlagsResultAsync(shell, title, selected, labelSelector, timeout, ct).ConfigureAwait(false);
            ThrowIfSystemCancel(result.ResultKind);
            return result.IsOk ? result.Value : null;
        }

        /// <inheritdoc cref="MultiSelectFlagsResultAsync{TEnum}(ICliAppShell, string, TEnum, Func{TEnum, string}?, TimeSpan?, CancellationToken)"/>
        public static Task<TigerTuiResult<TEnum>> MultiSelectFlagsResultAsync<TEnum>(
            string title,
            TEnum selected = default,
            Func<TEnum, string>? labelSelector = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
            where TEnum : struct, Enum
        {
            return MultiSelectFlagsResultAsync(InlineShell.Instance, title, selected, labelSelector, timeout, ct);
        }

        /// <summary>Selects flags and preserves the exact dialog result.</summary>
        /// <typeparam name="TEnum">The flags enum type.</typeparam>
        /// <param name="shell">The shell that hosts the prompt.</param>
        /// <param name="title">The prompt title.</param>
        /// <param name="selected">The flags selected initially.</param>
        /// <param name="labelSelector">An optional function that produces each flag label.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// A result containing the selected flags and exact outcome. Interactive and semi-interactive
        /// shells render the prompt; non-interactive shells do not read input and return
        /// <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static async Task<TigerTuiResult<TEnum>> MultiSelectFlagsResultAsync<TEnum>(
            ICliAppShell shell,
            string title,
            TEnum selected = default,
            Func<TEnum, string>? labelSelector = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
            where TEnum : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(shell);

            if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false))
                throw new InvalidOperationException($"{typeof(TEnum).Name} must be marked with FlagsAttribute.");

            var values = new List<TEnum>();
            var labels = new List<string>();
            var preselectedIndexes = new List<int>();
            var seenBits = new HashSet<ulong>();
            ulong availableBits = 0;
            ulong selectedBits = ToUInt64(selected);

            foreach (var value in Enum.GetValues<TEnum>())
            {
                ulong bits = ToUInt64(value);
                if (!IsSingleBit(bits) || !seenBits.Add(bits))
                    continue;

                int index = values.Count;
                values.Add(value);
                labels.Add(labelSelector?.Invoke(value) ?? value.ToString());
                availableBits |= bits;

                if ((selectedBits & bits) == bits)
                    preselectedIndexes.Add(index);
            }

            if ((selectedBits & ~availableBits) != 0)
                throw new ArgumentException("Selected flags contain bits that are not represented by selectable single-bit enum values.", nameof(selected));

            var indexesResult = await MultiSelectIndexesResultAsync(shell, title, labels, preselectedIndexes, timeout: timeout, ct: ct).ConfigureAwait(false);
            if (!indexesResult.IsOk)
                return TigerTuiResult<TEnum>.FromKind(indexesResult.ResultKind);

            ulong resultBits = 0;
            foreach (int index in indexesResult.Value!)
                resultBits |= ToUInt64(values[index]);

            return TigerTuiResult<TEnum>.Ok((TEnum)Enum.ToObject(typeof(TEnum), resultBits));
        }

        /// <inheritdoc cref="SelectFolderAsync(ICliAppShell, string, string?, IFolderBrowser?, TimeSpan?, CancellationToken)"/>
        public static Task<string?> SelectFolderAsync(
            string title,
            string? initialPath = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return SelectFolderAsync(InlineShell.Instance, title, initialPath, browser: null, timeout, ct);
        }

        /// <summary>
        /// Runs the folder picker on the default semi-interactive shell against a caller-supplied
        /// <paramref name="browser"/>. Lets a consumer drive the default shell with a custom
        /// (e.g. in-memory) navigation policy instead of the real filesystem.
        /// </summary>
        public static Task<string?> SelectFolderAsync(
            string title,
            string? initialPath,
            IFolderBrowser browser,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(browser);
            return SelectFolderAsync(InlineShell.Instance, title, initialPath, browser, timeout, ct);
        }

        /// <summary>Selects a folder path.</summary>
        /// <param name="shell">The shell that hosts the folder picker.</param>
        /// <param name="title">The picker title.</param>
        /// <param name="initialPath">The path shown initially, or <c>null</c> to use the browser root.</param>
        /// <param name="browser">The folder navigation provider, or <c>null</c> to use the local filesystem.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the picker.</param>
        /// <returns>
        /// The selected folder path, or <c>null</c> when no folder is selected. Interactive and
        /// semi-interactive shells render the picker; non-interactive shells do not read input and
        /// return <c>null</c>.
        /// </returns>
        public static async Task<string?> SelectFolderAsync(
            ICliAppShell shell,
            string title,
            string? initialPath = null,
            IFolderBrowser? browser = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            var result = await SelectFolderResultAsync(shell, title, initialPath, browser, timeout, ct).ConfigureAwait(false);
            ThrowIfSystemCancel(result.ResultKind);
            return result.IsOk ? result.Value : null;
        }

        /// <inheritdoc cref="SelectFolderResultAsync(ICliAppShell, string, string?, IFolderBrowser?, TimeSpan?, CancellationToken)"/>
        public static Task<TigerTuiResult<string>> SelectFolderResultAsync(
            string title,
            string? initialPath = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return SelectFolderResultAsync(InlineShell.Instance, title, initialPath, browser: null, timeout, ct);
        }

        /// <summary>Selects a folder path and preserves the exact dialog result.</summary>
        /// <param name="shell">The shell that hosts the folder picker.</param>
        /// <param name="title">The picker title.</param>
        /// <param name="initialPath">The path shown initially, or <c>null</c> to use the browser root.</param>
        /// <param name="browser">The folder navigation provider, or <c>null</c> to use the local filesystem.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the picker.</param>
        /// <returns>
        /// A result containing the selected folder path and exact outcome. Interactive and
        /// semi-interactive shells render the picker; non-interactive shells do not read input and
        /// return <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static async Task<TigerTuiResult<string>> SelectFolderResultAsync(
            ICliAppShell shell,
            string title,
            string? initialPath = null,
            IFolderBrowser? browser = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);

            var control = new InlineFolderSelect(shell, browser ?? new FileSystemFolderBrowser(), initialPath);
            var dialog = new InlineDialog(shell, title, control);
            var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
            return dr.Kind == DialogResultKind.Ok && dr.Payload is string path
                ? TigerTuiResult<string>.Ok(path)
                : TigerTuiResult<string>.FromKind(dr.Kind);
        }

        /// <inheritdoc cref="MessageBoxAsync(ICliAppShell, string, MessageBoxButtons, MessageBoxKind, string?, TimeSpan?, CancellationToken)"/>
        public static Task<DialogResultKind> MessageBoxAsync(
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            MessageBoxKind kind = MessageBoxKind.Normal,
            string? title = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MessageBoxAsync(InlineShell.Instance, message, buttons, kind, title, timeout, ct);
        }

        /// <summary>Displays a message box and returns the action used to close it.</summary>
        /// <param name="shell">The shell that hosts the message box.</param>
        /// <param name="message">The message to display.</param>
        /// <param name="buttons">The buttons to display.</param>
        /// <param name="kind">The semantic kind that selects the message-box surface.</param>
        /// <param name="title">The optional dialog title.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the message box.</param>
        /// <returns>
        /// The dialog result. Interactive and semi-interactive shells render the message box;
        /// non-interactive shells do not read input and return
        /// <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static async Task<DialogResultKind> MessageBoxAsync(
            ICliAppShell shell,
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            MessageBoxKind kind = MessageBoxKind.Normal,
            string? title = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);

            var control = new InlineMessageBoxControl(shell, message, buttons, kind: kind);
            var dialog = new InlineDialog(shell, title, control);
            var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
            ThrowIfSystemCancel(dr.Kind);
            return dr.Kind;
        }

        /// <summary>Convenience wrapper over <see cref="MessageBoxAsync(string, MessageBoxButtons, MessageBoxKind, string?, TimeSpan?, CancellationToken)"/>
        /// that shows the message on the warning surface (<see cref="MessageBoxKind.Warning"/>).</summary>
        public static Task<DialogResultKind> WarningAsync(
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            string? title = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MessageBoxAsync(InlineShell.Instance, message, buttons, MessageBoxKind.Warning, title, timeout, ct);
        }

        /// <summary>Shell-injected variant of <see cref="WarningAsync(string, MessageBoxButtons, string?, TimeSpan?, CancellationToken)"/>.</summary>
        public static Task<DialogResultKind> WarningAsync(
            ICliAppShell shell,
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            string? title = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MessageBoxAsync(shell, message, buttons, MessageBoxKind.Warning, title, timeout, ct);
        }

        /// <summary>Convenience wrapper over <see cref="MessageBoxAsync(string, MessageBoxButtons, MessageBoxKind, string?, TimeSpan?, CancellationToken)"/>
        /// that shows the message on the error surface (<see cref="MessageBoxKind.Error"/>).</summary>
        public static Task<DialogResultKind> ErrorAsync(
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            string? title = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MessageBoxAsync(InlineShell.Instance, message, buttons, MessageBoxKind.Error, title, timeout, ct);
        }

        /// <summary>Shell-injected variant of <see cref="ErrorAsync(string, MessageBoxButtons, string?, TimeSpan?, CancellationToken)"/>.</summary>
        public static Task<DialogResultKind> ErrorAsync(
            ICliAppShell shell,
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            string? title = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return MessageBoxAsync(shell, message, buttons, MessageBoxKind.Error, title, timeout, ct);
        }

        /// <inheritdoc cref="ConfirmAsync(ICliAppShell, string, bool?, TimeSpan?, CancellationToken)"/>
        public static Task<bool?> ConfirmAsync(
            string title,
            bool? preselect = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return ConfirmAsync(InlineShell.Instance, title, preselect, timeout, ct);
        }

        /// <summary>Asks a yes-or-no question.</summary>
        /// <param name="shell">The shell that hosts the confirmation prompt.</param>
        /// <param name="title">The question to display.</param>
        /// <param name="preselect"><c>true</c> to select Yes initially, <c>false</c> for No, or <c>null</c> for the default.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// <c>true</c> for Yes, <c>false</c> for No or dialog cancellation, or <c>null</c> for another
        /// unsuccessful outcome. Interactive and semi-interactive shells render the prompt;
        /// non-interactive shells do not read input and return <c>null</c>.
        /// </returns>
        public static async Task<bool?> ConfirmAsync(
            ICliAppShell shell,
            string title,
            bool? preselect = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            var result = await ConfirmResultAsync(shell, title, preselect, timeout, ct).ConfigureAwait(false);
            ThrowIfSystemCancel(result.ResultKind);

            return result.ResultKind switch
            {
                DialogResultKind.Yes => true,
                DialogResultKind.No => false,
                DialogResultKind.Cancel => false, // Escape / dialog cancel fallback
                _ => null,                         // Timeout, token cancellation, no result
            };
        }

        /// <inheritdoc cref="ConfirmResultAsync(ICliAppShell, string, bool?, TimeSpan?, CancellationToken)"/>
        public static Task<TigerTuiResult<bool>> ConfirmResultAsync(
            string title,
            bool? preselect = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            return ConfirmResultAsync(InlineShell.Instance, title, preselect, timeout, ct);
        }

        /// <summary>Asks a yes-or-no question and preserves the exact dialog result.</summary>
        /// <param name="shell">The shell that hosts the confirmation prompt.</param>
        /// <param name="title">The question to display.</param>
        /// <param name="preselect"><c>true</c> to select Yes initially, <c>false</c> for No, or <c>null</c> for the default.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <returns>
        /// A result containing the answer for Yes or No and the exact outcome. Interactive and
        /// semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static async Task<TigerTuiResult<bool>> ConfirmResultAsync(
            ICliAppShell shell,
            string title,
            bool? preselect = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(shell);

            // A Yes/No message box backs the confirmation. The question is the message body; the
            // default button follows the preselect (Yes/No), defaulting to Yes when unspecified.
            var defaultButton = preselect switch
            {
                true => DialogResultKind.Yes,
                false => DialogResultKind.No,
                _ => (DialogResultKind?)null,
            };

            var control = new InlineMessageBoxControl(shell, title, MessageBoxButtons.YesNo, defaultButton);
            var dialog = new InlineDialog(shell, title: null, control);
            var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);

            return dr.Kind switch
            {
                DialogResultKind.Yes => new TigerTuiResult<bool>(DialogResultKind.Yes, true),
                DialogResultKind.No => new TigerTuiResult<bool>(DialogResultKind.No, false),
                _ => TigerTuiResult<bool>.FromKind(dr.Kind),
            };
        }

        /// <inheritdoc cref="InputAsync(ICliAppShell, string, string?, TimeSpan?, CancellationToken, int?, Func{string, string?}?)"/>
        public static Task<string?> InputAsync(
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            return InputAsync(InlineShell.Instance, label, initialValue, timeout, ct, width, validator);
        }

        /// <summary>Prompts for a line of visible text.</summary>
        /// <param name="shell">The shell that hosts the input prompt.</param>
        /// <param name="label">The label displayed with the input.</param>
        /// <param name="initialValue">The initial input value.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <param name="width">The optional input width in cells.</param>
        /// <param name="validator">An optional function that returns a validation message, or <c>null</c> for valid input.</param>
        /// <returns>
        /// The entered text, or <c>null</c> when the prompt does not complete successfully. Interactive
        /// and semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <c>null</c>.
        /// </returns>
        public static Task<string?> InputAsync(
            ICliAppShell shell,
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            ArgumentNullException.ThrowIfNull(shell);

            return RunTextInputAsync(shell, label, initialValue, isSecret: false, timeout, ct, width, validator);
        }

        /// <inheritdoc cref="InputResultAsync(ICliAppShell, string, string?, TimeSpan?, CancellationToken, int?, Func{string, string?}?)"/>
        public static Task<TigerTuiResult<string>> InputResultAsync(
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            return InputResultAsync(InlineShell.Instance, label, initialValue, timeout, ct, width, validator);
        }

        /// <summary>Prompts for a line of visible text and preserves the exact dialog result.</summary>
        /// <param name="shell">The shell that hosts the input prompt.</param>
        /// <param name="label">The label displayed with the input.</param>
        /// <param name="initialValue">The initial input value.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <param name="width">The optional input width in cells.</param>
        /// <param name="validator">An optional function that returns a validation message, or <c>null</c> for valid input.</param>
        /// <returns>
        /// A result containing the entered text and exact outcome. Interactive and semi-interactive
        /// shells render the prompt; non-interactive shells do not read input and return
        /// <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static Task<TigerTuiResult<string>> InputResultAsync(
            ICliAppShell shell,
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            ArgumentNullException.ThrowIfNull(shell);

            return RunTextInputResultAsync(shell, label, initialValue, isSecret: false, timeout, ct, width, validator);
        }

        /// <inheritdoc cref="SecretInputAsync(ICliAppShell, string, string?, TimeSpan?, CancellationToken, int?, Func{string, string?}?)"/>
        public static Task<string?> SecretInputAsync(
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            return SecretInputAsync(InlineShell.Instance, label, initialValue, timeout, ct, width, validator);
        }

        /// <summary>Prompts for a line of masked text.</summary>
        /// <param name="shell">The shell that hosts the input prompt.</param>
        /// <param name="label">The label displayed with the input.</param>
        /// <param name="initialValue">The initial input value.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <param name="width">The optional input width in cells.</param>
        /// <param name="validator">An optional function that returns a validation message, or <c>null</c> for valid input.</param>
        /// <returns>
        /// The entered text, or <c>null</c> when the prompt does not complete successfully. Interactive
        /// and semi-interactive shells render the prompt; non-interactive shells do not read input and
        /// return <c>null</c>.
        /// </returns>
        public static Task<string?> SecretInputAsync(
            ICliAppShell shell,
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            ArgumentNullException.ThrowIfNull(shell);

            return RunTextInputAsync(shell, label, initialValue, isSecret: true, timeout, ct, width, validator);
        }

        /// <inheritdoc cref="SecretInputResultAsync(ICliAppShell, string, string?, TimeSpan?, CancellationToken, int?, Func{string, string?}?)"/>
        public static Task<TigerTuiResult<string>> SecretInputResultAsync(
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            return SecretInputResultAsync(InlineShell.Instance, label, initialValue, timeout, ct, width, validator);
        }

        /// <summary>Prompts for a line of masked text and preserves the exact dialog result.</summary>
        /// <param name="shell">The shell that hosts the input prompt.</param>
        /// <param name="label">The label displayed with the input.</param>
        /// <param name="initialValue">The initial input value.</param>
        /// <param name="timeout">The optional maximum time to wait for a response.</param>
        /// <param name="ct">A token that cancels the prompt.</param>
        /// <param name="width">The optional input width in cells.</param>
        /// <param name="validator">An optional function that returns a validation message, or <c>null</c> for valid input.</param>
        /// <returns>
        /// A result containing the entered text and exact outcome. Interactive and semi-interactive
        /// shells render the prompt; non-interactive shells do not read input and return
        /// <see cref="DialogResultKind.InteractionNotAllowed"/>.
        /// </returns>
        public static Task<TigerTuiResult<string>> SecretInputResultAsync(
            ICliAppShell shell,
            string label,
            string? initialValue = null,
            TimeSpan? timeout = default,
            CancellationToken ct = default,
            int? width = null,
            Func<string, string?>? validator = null)
        {
            ArgumentNullException.ThrowIfNull(shell);

            return RunTextInputResultAsync(shell, label, initialValue, isSecret: true, timeout, ct, width, validator);
        }

        private static async Task<string?> RunTextInputAsync(
            ICliAppShell shell,
            string label,
            string? initialValue,
            bool isSecret,
            TimeSpan? timeout,
            CancellationToken ct,
            int? width,
            Func<string, string?>? validator)
        {
            var result = await RunTextInputResultAsync(shell, label, initialValue, isSecret, timeout, ct, width, validator).ConfigureAwait(false);
            ThrowIfSystemCancel(result.ResultKind);
            return result.IsOk ? result.Value : null;
        }

        private static async Task<TigerTuiResult<string>> RunTextInputResultAsync(
            ICliAppShell shell,
            string label,
            string? initialValue,
            bool isSecret,
            TimeSpan? timeout,
            CancellationToken ct,
            int? width,
            Func<string, string?>? validator)
        {
            ArgumentNullException.ThrowIfNull(shell);

            var input = new InlineTextInput(shell, initialValue, isSecret, width, validator);
            var dialog = new InlineDialog(shell, title: null, input, label);
            var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
            return dr.Kind == DialogResultKind.Ok && dr.Payload is string text
                ? TigerTuiResult<string>.Ok(text)
                : TigerTuiResult<string>.FromKind(dr.Kind);
        }

        private static int[]? ResolvePreselectedIndexes<T>(
            IReadOnlyList<T> items,
            IReadOnlyCollection<T>? preselected,
            IEqualityComparer<T>? comparer)
        {
            if (preselected is null)
                return null;

            comparer ??= EqualityComparer<T>.Default;
            var indexes = new List<int>();
            foreach (var selectedItem in preselected)
            {
                int match = -1;
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!comparer.Equals(items[i], selectedItem))
                        continue;

                    match = i;
                    count++;
                }

                if (count == 0)
                    throw new ArgumentException("A preselected item was not found in the item list.", nameof(preselected));
                if (count > 1)
                    throw new ArgumentException("A preselected item matches more than one item.", nameof(preselected));
                if (!indexes.Contains(match))
                    indexes.Add(match);
            }

            indexes.Sort();
            return indexes.ToArray();
        }

        // Simple/adapter APIs collapse the modal outcome to value-or-null and cannot represent a
        // process/system cancellation (Ctrl-C / SIGINT / SIGTERM) without it masquerading as an ordinary
        // user cancel. They surface it as a clear exception instead; the rich *ResultAsync APIs return
        // SystemCancel as a normal result kind.
        private static void ThrowIfSystemCancel(DialogResultKind kind)
        {
            if (kind == DialogResultKind.SystemCancel)
                throw new TigerCliSystemCancellationException();
        }

        private static bool IsSingleBit(ulong value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }

        private static ulong ToUInt64<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            return Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum))) switch
            {
                TypeCode.SByte => unchecked((ulong)Convert.ToSByte(value)),
                TypeCode.Int16 => unchecked((ulong)Convert.ToInt16(value)),
                TypeCode.Int32 => unchecked((ulong)Convert.ToInt32(value)),
                TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(value)),
                TypeCode.Byte => Convert.ToByte(value),
                TypeCode.UInt16 => Convert.ToUInt16(value),
                TypeCode.UInt32 => Convert.ToUInt32(value),
                TypeCode.UInt64 => Convert.ToUInt64(value),
                _ => throw new InvalidOperationException("Unsupported enum underlying type.")
            };
        }
    }
}
