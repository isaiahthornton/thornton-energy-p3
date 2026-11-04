namespace ThorntonEnergy.Chat.Api;

/// <summary>The bot's name and the public facts both versions of the bot may share.</summary>
internal static class ChatPersona
{
    // Change the name here (and BOT_NAME in client/src/app/services/chat-service.ts)
    public const string BotName = "Wattson";

    // Only facts that are already on the public website. The bot must not invent anything beyond these.
    public const string CompanyFacts = """
        - Thornton Energy is an electric utility serving about 48,000 homes and businesses in East Windsor, Hightstown, Cranbury, Robbinsville, Plainsboro and Millstone, New Jersey.
        - 24/7 outage line: 1-800-555-1234. Customer care: 1-800-555-0100, Monday to Friday, 8 am to 6 pm. Email: support@thornenergy.com.
        - The Contact page has a message form. Messages get a reply within one business day.
        - Customers with an account can log in to see bills, usage, meters and outages, report an outage, pay a bill, and compare or switch rate plans.
        - New customers register with the account number printed on their bill (it looks like TE-100001).
        """;
}
