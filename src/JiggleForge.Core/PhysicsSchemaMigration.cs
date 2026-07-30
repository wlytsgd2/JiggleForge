namespace JiggleForge.Core;

public static class PhysicsSchemaMigration
{
    public static double HoldFrequencyFromV1(double grabSpring) =>
        Math.Clamp(2.0 + grabSpring * (200.0 / 3.0), 0.0, 60.0);

    public static double ReleaseFrequencyFromV1(double releaseSpring) =>
        Math.Clamp(0.2 + releaseSpring * 20.0, 0.0, 60.0);

    public static double ReleaseImpulseFromV1(double releaseKick) =>
        Math.Clamp(releaseKick * 0.4, 0.0, 10.0);

    public static double TargetFollowSecondsFromV1(double targetFollow)
    {
        double followSpeed = Math.Clamp(targetFollow, 0.0, 1.0);
        return followSpeed <= 0.000001
            ? 0.1
            : Math.Clamp(0.006 / followSpeed, 0.005, 0.1);
    }

    public static double GrabSpringForV1(double holdFrequencyHz) =>
        Math.Max(0.0, (holdFrequencyHz - 2.0) * 0.015);

    public static double ReleaseSpringForV1(double releaseFrequencyHz) =>
        Math.Max(0.0, (releaseFrequencyHz - 0.2) / 20.0);

    public static double ReleaseKickForV1(double releaseImpulse) =>
        Math.Max(0.0, releaseImpulse / 0.4);

    public static double TargetFollowForV1(double targetFollowSeconds) =>
        targetFollowSeconds <= 0.000001
            ? 1.0
            : Math.Clamp(0.006 / targetFollowSeconds, 0.0, 1.0);
}
