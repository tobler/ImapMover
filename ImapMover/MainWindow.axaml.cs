using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;

namespace ImapMover;

public partial class MainWindow : Window
{
    private int countLines = 0;
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {

        errormessage.Text = "";
        progressBar.Value = 0;
        countLines = 0;
        if (string.IsNullOrEmpty(SourceImapServer.Text))
        {
            errormessage.Text = "Source IMAP Server is required";
            return;
        }
        if (string.IsNullOrEmpty(SourceImapServerPort.Text))
        {
            errormessage.Text = "Source IMAP Port is required";
            return;
        }
        if (string.IsNullOrEmpty(SourceUsername.Text))
        {
            errormessage.Text = "Source IMAP Username is required";
            return;
        }
        if (string.IsNullOrEmpty(SourcePassword.Text))
        {
            errormessage.Text = "Source IMAP Password is required";
            return;
        }
        if (string.IsNullOrEmpty(DestinationImapServer.Text))
        {
            errormessage.Text = "Destination IMAP Server is required";
            return;
        }
        if (string.IsNullOrEmpty(DestinationImapServerPort.Text))
        {
            errormessage.Text = "Destination IMAP Port is required";
            return;
        }
        if (string.IsNullOrEmpty(DestinationUsername.Text))
        {
            errormessage.Text = "Destination IMAP Username is required";
            return;
        }
        if (string.IsNullOrEmpty(DestinationPassword.Text))
        {
            errormessage.Text = "Destination IMAP Password is required";
            return;
        }

        var sourceclient = new ImapClient();
        var b = ConnectToServer(sourceclient, SourceImapServer.Text, SourceUsername.Text, SourcePassword.Text, Convert.ToInt32(SourceImapServerPort.Text), SourceImapServerUseSSL.IsChecked.Value);
        if (!b || !sourceclient.IsConnected)
        {
            errormessage.Text = "Could not connect to source server";
            return;
        }

        var destinationclient = new ImapClient();
        b = ConnectToServer(destinationclient, DestinationImapServer.Text, DestinationUsername.Text, DestinationPassword.Text, Convert.ToInt32(DestinationImapServerPort.Text), DestinationImapServerUseSSL.IsChecked.Value);
        if (!b || !destinationclient.IsConnected)
        {
            errormessage.Text = "Could not connect to destination server";
            return;
        }

        sourceclient.Disconnect(true);
        destinationclient.Disconnect(true);

        StartButton.IsEnabled = false;
        var source = new ConnectionDetailsClass(SourceImapServer.Text, Convert.ToInt32(SourceImapServerPort.Text), SourceUsername.Text, SourcePassword.Text, SourceImapServerUseSSL.IsChecked == true);
        var destination = new ConnectionDetailsClass(DestinationImapServer.Text, Convert.ToInt32(DestinationImapServerPort.Text), DestinationUsername.Text, DestinationPassword.Text, DestinationImapServerUseSSL.IsChecked == true);
        var res = Task.Run(async () => await StartMigrationAsync(source, destination));
    }

    private async Task StartMigrationAsync(ConnectionDetailsClass source, ConnectionDetailsClass destination)
    {
        try
        {
            EstablishImapConnection(source, destination);
            var progress = new ProgressInfo();
            var inbox = source.client.Inbox;
            inbox.Open(FolderAccess.ReadOnly);
            var personal = source.client.GetFolder(source.client.PersonalNamespaces[0]);
            Log("Calculating Mails. Please wait...");

            await ListFolders(source.client, new FolderNamespace('.', ""), 0, progress);
            Log($"Starting migration. A total of {progress.TotalEmails} emails to copy.");

            await MigrateFolders(source, destination, new FolderNamespace('.', ""), 0, progress);

            await source.client.DisconnectAsync(true);
            await destination.client.DisconnectAsync(true);

            Log("Migration completed!");
        }
        catch (Exception ex)
        {
            Log("An error occurred: " + ex.Message);
        }
        EnableStartButton();
    }

    private void EnableStartButton()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StartButton.IsEnabled = true;
        });
    }

    private async Task ListFolders(ImapClient sourceClient, FolderNamespace folderNameSpace, int i, ProgressInfo progress)
    {
        var folders = await sourceClient.GetFoldersAsync(folderNameSpace);
        i++;
        foreach (var folder in folders)
        {
            await folder.OpenAsync(FolderAccess.ReadOnly);
            // Add the total number of messages in the current folder
            var uids = await folder.SearchAsync(SearchQuery.All);
            progress.TotalEmails += uids.Count;
            // Closing the source folder
            await folder.CloseAsync();

            var c = new String(' ', i);
            Log(string.Format("{0} {1} - {2} emails", c, folder.Name, uids.Count));
            await ListFolders(sourceClient, new FolderNamespace('.', folder.FullName), i, progress);
        }
    }

    private async Task MigrateFolders(ConnectionDetailsClass source, ConnectionDetailsClass destination, FolderNamespace folderNameSpace, int i, ProgressInfo progress)
    {
        var folders = await source.client.GetFoldersAsync(folderNameSpace);
        var rootfolder = destination.client.GetFolder(folderNameSpace);
        i++;
        foreach (var folder in folders)
        {
            await folder.OpenAsync(FolderAccess.ReadOnly);

            IMailFolder destinationFolder;
            try
            {
                destinationFolder = destination.client.GetFolder(folder.FullName);
                Log("Folder already exists -" + folder.FullName);
            }
            catch (FolderNotFoundException)
            {
                destinationFolder = await rootfolder.CreateAsync(folder.Name, true);
                Log("Folder created -" + folder.FullName);
            }

            // Add the total number of messages in the current folder
            var uids = await folder.SearchAsync(SearchQuery.All);
            await folder.CloseAsync();

            var c = new String(' ', i);
            Log(string.Format("{0} Copy email from: {1} ", c, folder.Name));

            await CopyMails(source, destination, folder, destinationFolder, progress);

            await MigrateFolders(source, destination, new FolderNamespace('.', folder.FullName), i, progress);
        }
    }

    private async Task CopyMails(ConnectionDetailsClass source, ConnectionDetailsClass destination, IMailFolder sourceFolder, IMailFolder destinationFolder, ProgressInfo progress)
    {
        // Open the source folder in read mode
        await sourceFolder.OpenAsync(FolderAccess.ReadOnly);

        // Open or create the destination folder
        await destinationFolder.OpenAsync(FolderAccess.ReadWrite);
        var existingMessageIds = new HashSet<string>();

        Log("Retrieve already existing message ids in destination folder");
        int u = 0;
        foreach (var uid in await destinationFolder.SearchAsync(SearchQuery.All))
        {
            try
            {
                u++;
                if (u % 50 == 0)
                {
                    Log($"Retrieved {u} messages from destination folder {destinationFolder.FullName}");
                }

                var message = await destinationFolder.GetMessageAsync(uid);
                if (message.MessageId != null)
                {
                    existingMessageIds.Add(message.MessageId);
                }
            }
            catch (Exception ex)
            {
                Log($"Error while retrieving message with uid {uid}: {ex.Message}");
                EstablishImapConnection(source, destination);
                await sourceFolder.OpenAsync(FolderAccess.ReadOnly);
                await destinationFolder.OpenAsync(FolderAccess.ReadWrite);
            }
        }
        Log($"Retrieved {u} messages from destination folder {destinationFolder.FullName}");

        // Retrieve existing UIDs in the destination folder
        var destinationUids = await destinationFolder.SearchAsync(SearchQuery.All);

        // Copy mails that are not yet in the destination folder
        var sourceUids = await sourceFolder.SearchAsync(SearchQuery.All);
        foreach (var uid in sourceUids)
        {
            progress.CopiedEmails++;
            try
            {
                double percentComplete = (double)progress.CopiedEmails / progress.TotalEmails * 100;
                var message = await sourceFolder.GetMessageAsync(uid);
                if (message.MessageId != null && !existingMessageIds.Contains(message.MessageId))
                {
                    await destinationFolder.AppendAsync(message);
                    Log($"Copied: {message.MessageId} - {destinationFolder.FullName} - {sourceFolder.FullName} - {progress.CopiedEmails}/{progress.TotalEmails} ({percentComplete:0.00}%)");
                }
                else
                {
                    Log($"Message-Id {message.MessageId} already exists - skipped. {destinationFolder.FullName}");
                }
                SetPercentage(percentComplete);
            }
            catch (Exception ex)
            {
                Log($"Error while copying the message with UID {uid}: {ex.Message}");
                EstablishImapConnection(source, destination);
                await sourceFolder.OpenAsync(FolderAccess.ReadOnly);
                await destinationFolder.OpenAsync(FolderAccess.ReadWrite);
            }
        }

        // Close the source folder
        await sourceFolder.CloseAsync();
        // Close the destination folder
        await destinationFolder.CloseAsync();
    }

    private void SetPercentage(double percentComplete)
    {
        Dispatcher.UIThread.Post(() =>
        {
            progressBar.Value = percentComplete;
        });
    }

    private bool ConnectToServer(ImapClient client, string imapServer, string username, string password, int port, bool useSsl)
    {
        Log($"Try to connect to {imapServer}");
        try
        {
            client.Connect(imapServer, port, useSsl);
            client.Authenticate(username, password);
        }
        catch (Exception ex)
        {
            Log($"Connection failed to {imapServer} - {ex.Message}");
            return false;
        }
        Log($"Connection successful to {imapServer}");
        return true;
    }

    private void EstablishImapConnection(ConnectionDetailsClass source, ConnectionDetailsClass destination)
    {
        try
        {
            if (!source.client.IsConnected)
            {
                ConnectToServer(source.client, source.ImapServer, source.ImapUsername, source.ImapPassword, source.ImapPort, source.UseSSL);
            }

            if (!destination.client.IsConnected)
            {
                ConnectToServer(destination.client, destination.ImapServer, destination.ImapUsername, destination.ImapPassword, destination.ImapPort, destination.UseSSL);
            }
        }
        catch (Exception ex)
        {
            Log("An error has occured: " + ex.Message);
        }
    }
  
    private void Log(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (countLines>500)
            {
                logmessages.Text = "";
                countLines = 0;
            }
            logmessages.Text += message + Environment.NewLine;

            // Set the CaretIndex to the end of the text to scroll down
            logmessages.CaretIndex = logmessages.Text.Length;
            countLines++;
        });
    }
}