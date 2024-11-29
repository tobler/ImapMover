# ImapMover

ImapMover is a C# program that uses the MailKit library to migrate emails from one IMAP server to another. It is developed using the Avalonia framework for its user interface, providing a simple way to move emails between different mail servers.

## Functionality

The program allows you to connect to a source IMAP server and a destination IMAP server and copy the emails from one to the other. It is a graphical application that offers the following main features:

1. **Connect to IMAP Servers:** The software allows the input of server details, such as IMAP server address, port, username, and password, for both the source and destination servers.

2. **Email Migration:** After successfully connecting to both servers, the application starts the migration process. It traverses the folder structure of the source server and copies emails to the corresponding folders on the destination server.

3. **Logging:** During the copying process, progress and any errors are displayed in the log. The log scrolls automatically to display the latest entries.

## Tools Used

- **C# 12.0:** The programming language in which the project is implemented.
- **.NET 8.0:** The target framework for the project.
- **Avalonia:** A cross-platform UI framework for C#.
- **MailKit:** A popular library for working with IMAP and SMTP servers in .NET.

## Usage

1. **Set Up the Project:**
   - Ensure that .NET 8.0 is installed on your system.
   - Clone the repository and open the project with JetBrains Rider or another supported IDE.

2. **Build and Run:**
   - Build the project in your IDE.
   - Run the application.

3. **Perform Migration:**
   - Enter the details of the source IMAP server: address, port, username, password, and SSL option.
   - Enter the details of the destination IMAP server.
   - Click the button to start the migration.
   - Monitor the progress in the log section of the program.

For questions or issues, please open an issue in the repository. Good luck with your email migration!
