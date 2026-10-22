namespace BetterMalodyV.Shared;

[Flags]
public enum StatusMask { None = 0, Alpha = 1, Beta = 2, Stable = 4, All = 7 }

public static class StatusSelection
{
    // The local protocol stores 0/1/2. Promotion is an online recommendation,
    // not a fourth RankStatus. Never classify Unknown=10 / Update=11 as Alpha.
    public static bool Matches(StatusMask mask, uint rankStatus) => mask == StatusMask.All
        || (rankStatus <= 2 && ((int)mask & (1 << (int)rankStatus)) != 0);
}
