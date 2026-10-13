namespace IzzyBlazor;

/// <summary>What <see cref="Stepper"/> shares with its <see cref="Step"/>s.</summary>
internal sealed class StepperContext(Stepper owner)
{
    private readonly List<Step> _steps = [];

    public int Current => owner.Current;

    public string CompletedLabel => owner.EffectiveCompletedLabel;

    /// <summary>
    /// The step's position, counting from 1, among the visible steps in the order they were first
    /// rendered. (Blazor renders a step inserted later after the existing ones, so conditional
    /// steps use Step.Hidden rather than @if to keep the order.)
    /// </summary>
    public int NumberOf(Step step)
    {
        var number = 0;
        foreach (var other in _steps)
        {
            if (!other.Hidden)
            {
                number++;
            }

            if (ReferenceEquals(other, step))
            {
                return number;
            }
        }

        return 0;
    }

    public void Register(Step step)
    {
        if (!_steps.Contains(step))
        {
            _steps.Add(step);
        }
    }

    public void Unregister(Step step) => _steps.Remove(step);
}
