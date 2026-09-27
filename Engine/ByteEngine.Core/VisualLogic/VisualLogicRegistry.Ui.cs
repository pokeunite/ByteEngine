using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterUi(VisualLogicRegistry registry)
    {
        void Action(string id, string label,
            Action<VisualInstruction, EventExecutionContext> execute) =>
            registry.RegisterAction(new VisualActionDefinition
            {
                Id = id, Category = "UI", DisplayName = label, Execute = execute
            });
        void Condition(string id, string label,
            Func<VisualInstruction, EventExecutionContext, bool> evaluate) =>
            registry.RegisterCondition(new VisualConditionDefinition
            {
                Id = id, Category = "UI", DisplayName = label, Evaluate = evaluate
            });

        Action("ui.setText", "Set Text", (i, c) =>
        {
            if (ResolveUiTarget(i, c)?.GetComponent<UiText>() is { } text)
                text.Text = EventValueResolver.GetString(i, "text", c);
        });
        Action("ui.setTextFromHealth", "Set Text From Health", (i, c) =>
        {
            UiText? text = ResolveUiTarget(i, c)?.GetComponent<UiText>();
            HealthComponent? health = ResolveObjectArgument(i, "source", c, false)?
                .GetComponent<HealthComponent>();
            if (text != null && health != null)
                text.Text = $"{EventValueResolver.GetString(i, "prefix", c)}{health.CurrentHealth:0}/{health.MaxHealth:0}";
        });
        Action("ui.setButtonLabel", "Set Button Label", (i, c) =>
        {
            if (ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is { Kind: UiWidgetKind.Button } button)
                button.Label = EventValueResolver.GetString(i, "text", c);
        });
        Action("ui.setImage", "Set Image", (i, c) =>
        {
            UiWidget? image = ResolveUiTarget(i, c)?.GetComponent<UiWidget>();
            if (image is not { Kind: UiWidgetKind.Image }) return;
            string token = EventValueResolver.GetString(i, "image", c);
            image.ImageReference = Guid.TryParse(token, out Guid id)
                ? new AssetReference(id)
                : string.IsNullOrWhiteSpace(token) || Path.IsPathRooted(token)
                    ? AssetReference.Empty : new AssetReference(token);
        });
        Action("ui.focus", "Focus Button", (i, c) =>
        {
            if (ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.Button } button) UiNavigation.Focus(button);
        });
        Action("ui.setBarValue", "Set Bar Value", (i, c) =>
        {
            if (ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is { Kind: UiWidgetKind.ProgressBar } bar)
                bar.Value = Math.Max(0f, (float)EventValueResolver.GetNumber(i, "value", c));
        });
        Action("ui.setBarMaximum", "Set Bar Maximum", (i, c) =>
        {
            if (ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is { Kind: UiWidgetKind.ProgressBar } bar)
                bar.Maximum = Math.Max(.001f, (float)EventValueResolver.GetNumber(i, "value", c, 100));
        });
        Action("ui.setBarFromHealth", "Set Bar From Health", (i, c) =>
        {
            UiWidget? bar = ResolveUiTarget(i, c)?.GetComponent<UiWidget>();
            GameObject? source = ResolveObjectArgument(i, "source", c, false);
            HealthComponent? health = source?.GetComponent<HealthComponent>();
            if (bar is { Kind: UiWidgetKind.ProgressBar } && health != null)
            {
                bar.Maximum = health.MaxHealth;
                bar.Value = health.CurrentHealth;
            }
        });
        Action("ui.show", "Show UI", (i, c) => SetUiVisible(i, c, true));
        Action("ui.hide", "Hide UI", (i, c) => SetUiVisible(i, c, false));
        Action("ui.setButtonEnabled", "Set Button Enabled", (i, c) =>
        {
            if (ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is { Kind: UiWidgetKind.Button } button)
                button.Interactable = EventValueResolver.GetBoolean(i, "enabled", c, true);
        });
        Condition("ui.buttonClicked", "On Button Clicked", (i, c) =>
            ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.Button } button && button.WasClicked);
        Condition("ui.buttonHovered", "Is Button Hovered", (i, c) =>
            ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.Button } button && button.IsHovered);
        Condition("ui.buttonFocused", "Is Button Focused", (i, c) =>
            ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.Button } button && button.IsFocused);
        Condition("ui.buttonEnabled", "Is Button Enabled", (i, c) =>
            ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.Button, Interactable: true });
        Condition("ui.isVisible", "Is UI Visible", (i, c) =>
        {
            GameObject? target = ResolveUiTarget(i, c);
            return target != null && UiLayout.IsVisible(target) &&
                (target.GetComponent<UiText>() is { Visible: true } ||
                target.GetComponent<UiWidget>() is { Visible: true } ||
                target.GetComponent<UiCanvas>() is { Enabled: true });
        });
        Condition("ui.barAbove", "Is Bar Above Value", (i, c) =>
            ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.ProgressBar } bar &&
            bar.Value > EventValueResolver.GetNumber(i, "value", c));
        Condition("ui.barBelow", "Is Bar Below Value", (i, c) =>
            ResolveUiTarget(i, c)?.GetComponent<UiWidget>() is
                { Kind: UiWidgetKind.ProgressBar } bar &&
            bar.Value < EventValueResolver.GetNumber(i, "value", c));
    }

    private static GameObject? ResolveUiTarget(
        VisualInstruction instruction, EventExecutionContext context) =>
        ResolveObjectTarget(instruction, context, false);

    private static void SetUiVisible(
        VisualInstruction instruction, EventExecutionContext context, bool visible)
    {
        GameObject? target = ResolveUiTarget(instruction, context);
        if (target?.GetComponent<UiText>() is { } text) text.Visible = visible;
        if (target?.GetComponent<UiWidget>() is { } widget) widget.Visible = visible;
        if (target?.GetComponent<UiCanvas>() is { } canvas) canvas.Enabled = visible;
    }
}
