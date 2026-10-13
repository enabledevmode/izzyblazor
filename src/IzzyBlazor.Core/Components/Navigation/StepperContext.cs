namespace IzzyBlazor;

/// <summary>What <see cref="Stepper"/> shares with its <see cref="Step"/>s.</summary>
internal sealed class StepperContext(Stepper owner)
{
    private readonly List<Step> _steps = [];

    public int Current => owner.Current;

    public string CompletedLabel => owner.CompletedLabel;

    /// <summary>The step's position, counting from 1, in the order the steps were first rendered.</summary>
    public int NumberOf(Step step)
    {
        var index = _steps.IndexOf(step);
        return index < 0 ? 0 : index + 1;
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
