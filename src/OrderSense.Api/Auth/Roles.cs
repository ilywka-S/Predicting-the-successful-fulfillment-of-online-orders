namespace OrderSense.Api.Auth;

public static class Roles
{
    public const string Manager = "manager";
    public const string Analyst = "analyst";
    public const string Admin = "admin";

    public static readonly string[] All = [Manager, Analyst, Admin];
}