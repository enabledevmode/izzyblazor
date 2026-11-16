namespace {{RootNamespace}};

public static class Setup
{
    public const string Interactivity = "{{Interactivity}}";
#if (AllInteractive)
    public const bool Everywhere = true;
#endif
}
