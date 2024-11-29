using MailKit.Net.Imap;

namespace ImapMover;

public class ConnectionDetailsClass
{
    public string ImapServer { get; set; }
    public int ImapPort { get; set; }
    public string ImapUsername { get; set; }
    public string ImapPassword { get; set; }
    public bool UseSSL { get; set; }
    public ImapClient client = new ImapClient();

    public ConnectionDetailsClass(string imapServer, int imapPort, string imapUsername, string imapPassword, bool useSSL)
    {
        ImapServer = imapServer;
        ImapPort = imapPort;
        ImapUsername = imapUsername;
        ImapPassword = imapPassword;
        UseSSL = useSSL;
    }
}