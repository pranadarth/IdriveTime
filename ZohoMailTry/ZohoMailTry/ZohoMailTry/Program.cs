using System;
using System.Collections.Generic;

namespace SimpleMailingSystem
{
    // --- Data Models ---

    class User
    {
        public string Name;
        public string Email;
        public string Password;
        public List<int> InboxIds = new List<int>();
        public List<int> SentIds = new List<int>();
        public List<int> DeletedInboxIds = new List<int>();
        public List<int> DeletedSentIds = new List<int>();
        public List<string> SharedWithUserNames = new List<string>();
    }

    class Group
    {
        public string Name;
        public string Email;
        public string Password;
        public string Description;
        public List<string> MemberUserNames = new List<string>();
    }

    class Mail
    {
        public int Id;
        public string FromUserEmail;
        public string ToAddress;
        public string Subject;
        public string Content;
        public bool IsRecalled;
    }

    // --- Main Application ---

    class Program
    {
        static List<User> users = new List<User>();
        static List<Group> groups = new List<Group>();
        static List<Mail> mails = new List<Mail>();
        static int mailCounter = 1;

        static void Main(string[] args)
        {
            bool running = true;
            while (running)
            {
                PrintMenu();
                Console.Write("\nSelect any option : ");
                string choice = Console.ReadLine();
                Console.WriteLine();

                switch (choice)
                {
                    case "1": CreateUser(); break;
                    case "2": CreateGroup(); break;
                    case "3": GroupAssignment(); break;
                    case "4": ComposeMail(); break;
                    case "5": ViewInbox(); break;
                    case "6": ViewSentMails(); break;
                    case "7": DeleteMail(); break;
                    case "8": RecallMail(); break;
                    case "9": ShareInbox(); break;
                    case "10": running = false; break;
                    default: Console.WriteLine("Invalid option. Please try again."); break;
                }
            }
        }

        static void PrintMenu()
        {
            Console.WriteLine("\n--------------------------------");
            Console.WriteLine("1) Create User");
            Console.WriteLine("2) Create Group");
            Console.WriteLine("3) Group Assignment");
            Console.WriteLine("4) Compose Mail");
            Console.WriteLine("5) Inbox");
            Console.WriteLine("6) Sent Mail");
            Console.WriteLine("7) Delete Mail");
            Console.WriteLine("8) Recall");
            Console.WriteLine("9) Share Inbox");
            Console.WriteLine("10) Exit");
        }

        // --- 1. User Creation ---
        static void CreateUser()
        {
            Console.Write("Enter User Name : ");
            string name = Console.ReadLine();
            Console.Write("Enter Email ID : ");
            string email = Console.ReadLine();
            Console.Write("Enter Password : ");
            string pass = Console.ReadLine();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Name and Email cannot be empty.");
                return;
            }

            if (GetUserByName(name) != null || GetUserByEmail(email) != null)
            {
                Console.WriteLine("Error: User Name or Email already exists.");
                return;
            }

            User newUser = new User { Name = name, Email = email, Password = pass };
            users.Add(newUser);
            Console.WriteLine("User Created");
        }

        // --- 2. User Groups Creation ---
        static void CreateGroup()
        {
            Console.Write("Enter Group Name : ");
            string name = Console.ReadLine();
            Console.Write("Enter Group Mail ID : ");
            string email = Console.ReadLine();
            Console.Write("Enter Group Mail Password : ");
            string pass = Console.ReadLine();
            Console.Write("Enter Group Description : ");
            string desc = Console.ReadLine();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Group Name and Email cannot be empty.");
                return;
            }

            if (GetGroupByName(name) != null || GetGroupByEmail(email) != null)
            {
                Console.WriteLine("Error: Group Name or Email already exists.");
                return;
            }

            Group newGroup = new Group { Name = name, Email = email, Password = pass, Description = desc };
            groups.Add(newGroup);
            Console.WriteLine("Group Created");
        }

        // --- 3. Group Assignment ---
        static void GroupAssignment()
        {
            Console.Write("Enter Group Name : ");
            string gName = Console.ReadLine();
            Console.Write("Enter User Name : ");
            string uName = Console.ReadLine();
            Console.Write("Add / Remove : ");
            string action = Console.ReadLine();

            Group g = GetGroupByName(gName);
            User u = GetUserByName(uName);

            if (g == null) { Console.WriteLine("Error: Group not found."); return; }
            if (u == null) { Console.WriteLine("Error: User not found."); return; }

            if (action.ToLower() == "add")
            {
                bool exists = false;
                foreach (string member in g.MemberUserNames)
                {
                    if (member == uName) exists = true;
                }
                if (!exists)
                {
                    g.MemberUserNames.Add(uName);
                    Console.WriteLine("User added to Group");
                }
                else Console.WriteLine("User is already in the group.");
            }
            else if (action.ToLower() == "remove")
            {
                if (g.MemberUserNames.Remove(uName)) Console.WriteLine("User removed from Group");
                else Console.WriteLine("User is not in the group.");
            }
            else Console.WriteLine("Invalid action.");
        }

        // --- 4. Compose Mail ---
        static void ComposeMail()
        {
            Console.Write("Enter From User : ");
            string fromUserName = Console.ReadLine();
            Console.Write("Enter To Address : ");
            string toAddress = Console.ReadLine();
            Console.Write("Enter Subject : ");
            string subject = Console.ReadLine();
            Console.Write("Enter Content : ");
            string content = Console.ReadLine();

            User sender = GetUserByName(fromUserName);
            if (sender == null) { Console.WriteLine("Error: From User not found."); return; }

            // Validate To Address (Must be a User or Group)
            bool isValidTo = false;
            if (GetUserByEmail(toAddress) != null || GetGroupByEmail(toAddress) != null)
            {
                isValidTo = true;
            }

            if (!isValidTo)
            {
                Console.WriteLine("Error: 'To' address does not exist.");
                return;
            }

            // Create Mail Record
            Mail newMail = new Mail
            {
                Id = mailCounter++,
                FromUserEmail = sender.Email,
                ToAddress = toAddress,
                Subject = subject,
                Content = content,
                IsRecalled = false
            };
            mails.Add(newMail);

            // Add to Sender's Sent box
            sender.SentIds.Add(newMail.Id);

            // Add to Receiver's Inbox (User or all Group members)
            User toUser = GetUserByEmail(toAddress);
            if (toUser != null)
            {
                toUser.InboxIds.Add(newMail.Id);
            }
            else
            {
                Group toGroup = GetGroupByEmail(toAddress);
                if (toGroup != null)
                {
                    foreach (string memberName in toGroup.MemberUserNames)
                    {
                        User member = GetUserByName(memberName);
                        if (member != null) member.InboxIds.Add(newMail.Id);
                    }
                }
            }

            Console.WriteLine("Mail Sent.");
        }

        // --- 5. Viewing Inbox ---
        static void ViewInbox()
        {
            Console.Write("Enter User Name : ");
            string uName = Console.ReadLine();
            User u = GetUserByName(uName);
            if (u == null) { Console.WriteLine("Error: User not found."); return; }

            Console.WriteLine("S.No\tFrom\t\tTo\t\tSubject\t\tContent");
            List<Mail> displayMails = GetInboxDisplayList(u);
            for (int i = 0; i < displayMails.Count; i++)
            {
                Mail m = displayMails[i];
                Console.WriteLine($"{i + 1}\t{m.FromUserEmail}\t{m.ToAddress}\t{m.Subject}\t{m.Content}");
            }

            // Check Shared Inboxes
            foreach (User otherUser in users)
            {
                bool isSharedWithMe = false;
                foreach (string shared in otherUser.SharedWithUserNames)
                {
                    if (shared == uName) isSharedWithMe = true;
                }

                if (isSharedWithMe)
                {
                    Console.WriteLine($"\nShared Inbox of {otherUser.Name}");
                    Console.WriteLine("S.No\tFrom\t\tTo\t\tSubject\t\tContent");
                    List<Mail> sharedMails = GetInboxDisplayList(otherUser);
                    for (int i = 0; i < sharedMails.Count; i++)
                    {
                        Mail m = sharedMails[i];
                        Console.WriteLine($"{i + 1}\t{m.FromUserEmail}\t{m.ToAddress}\t{m.Subject}\t{m.Content}");
                    }
                }
            }
        }

        // --- 6. Viewing Sent Mails ---
        static void ViewSentMails()
        {
            Console.Write("Enter User Name : ");
            string uName = Console.ReadLine();
            User u = GetUserByName(uName);
            if (u == null) { Console.WriteLine("Error: User not found."); return; }

            Console.WriteLine("S.No\tTo\t\tSubject\t\tContent\t\tStatus");
            List<Mail> sentMails = GetSentDisplayList(u);
            for (int i = 0; i < sentMails.Count; i++)
            {
                Mail m = sentMails[i];
                string status = m.IsRecalled ? "Recalled" : "";
                Console.WriteLine($"{i + 1}\t{m.ToAddress}\t{m.Subject}\t{m.Content}\t{status}");
            }
        }

        // --- 7. Delete Mail ---
        static void DeleteMail()
        {
            Console.Write("Enter User Name : ");
            string uName = Console.ReadLine();
            User u = GetUserByName(uName);
            if (u == null) { Console.WriteLine("Error: User not found."); return; }

            Console.Write("From Inbox/Sent ? : ");
            string folder = Console.ReadLine().ToLower();

            if (folder == "inbox")
            {
                List<Mail> currentInbox = GetInboxDisplayList(u);
                Console.Write("Enter Serial number to delete from inbox: ");
                if (int.TryParse(Console.ReadLine(), out int sNo) && sNo > 0 && sNo <= currentInbox.Count)
                {
                    u.DeletedInboxIds.Add(currentInbox[sNo - 1].Id);
                    Console.WriteLine("Mail deleted.");
                }
                else Console.WriteLine("Invalid Serial Number.");
            }
            else if (folder == "sent")
            {
                List<Mail> currentSent = GetSentDisplayList(u);
                Console.Write("Enter Serial number to delete from sent mails: ");
                if (int.TryParse(Console.ReadLine(), out int sNo) && sNo > 0 && sNo <= currentSent.Count)
                {
                    u.DeletedSentIds.Add(currentSent[sNo - 1].Id);
                    Console.WriteLine("Mail deleted.");
                }
                else Console.WriteLine("Invalid Serial Number.");
            }
            else Console.WriteLine("Invalid folder specified.");
        }

        // --- 8. Recall Mail ---
        static void RecallMail()
        {
            Console.Write("Enter User Name : "); // Note: Added so the program knows WHO is recalling
            string uName = Console.ReadLine();
            User u = GetUserByName(uName);
            if (u == null) { Console.WriteLine("Error: User not found."); return; }

            List<Mail> sentMails = GetSentDisplayList(u);
            Console.Write("Enter the Serial number to Recall : ");

            if (int.TryParse(Console.ReadLine(), out int sNo) && sNo > 0 && sNo <= sentMails.Count)
            {
                Mail mToRecall = sentMails[sNo - 1];
                if (mToRecall.IsRecalled)
                {
                    Console.WriteLine("Mail is already recalled.");
                }
                else
                {
                    mToRecall.IsRecalled = true;
                    Console.WriteLine("Mail Recalled");
                }
            }
            else Console.WriteLine("Invalid Serial Number.");
        }

        // --- 9. Share Inbox ---
        static void ShareInbox()
        {
            Console.Write("Enter User Name : ");
            string ownerName = Console.ReadLine();
            Console.Write("Enter User to Share Inbox : ");
            string shareeName = Console.ReadLine();

            User owner = GetUserByName(ownerName);
            User sharee = GetUserByName(shareeName);

            if (owner == null || sharee == null)
            {
                Console.WriteLine("Error: One or both users do not exist.");
                return;
            }

            bool alreadyShared = false;
            foreach (string user in owner.SharedWithUserNames)
            {
                if (user == shareeName) alreadyShared = true;
            }

            if (!alreadyShared)
            {
                owner.SharedWithUserNames.Add(shareeName);
                Console.WriteLine("Inbox shared");
            }
            else
            {
                // Revoke logic
                owner.SharedWithUserNames.Remove(shareeName);
                Console.WriteLine("Inbox sharing revoked");
            }
        }

        // --- Helper Methods (Replacing LINQ) ---

        static User GetUserByName(string name)
        {
            foreach (User u in users) if (u.Name == name) return u;
            return null;
        }

        static User GetUserByEmail(string email)
        {
            foreach (User u in users) if (u.Email == email) return u;
            return null;
        }

        static Group GetGroupByName(string name)
        {
            foreach (Group g in groups) if (g.Name == name) return g;
            return null;
        }

        static Group GetGroupByEmail(string email)
        {
            foreach (Group g in groups) if (g.Email == email) return g;
            return null;
        }

        static Mail GetMailById(int id)
        {
            foreach (Mail m in mails) if (m.Id == id) return m;
            return null;
        }

        // Retrieves Inbox mails in recent-first order, excluding deleted/recalled
        static List<Mail> GetInboxDisplayList(User u)
        {
            List<Mail> display = new List<Mail>();
            // Loop backwards for Recent-First ordering
            for (int i = u.InboxIds.Count - 1; i >= 0; i--)
            {
                int mailId = u.InboxIds[i];

                bool isDeleted = false;
                foreach (int delId in u.DeletedInboxIds) if (delId == mailId) isDeleted = true;

                if (!isDeleted)
                {
                    Mail m = GetMailById(mailId);
                    if (m != null && !m.IsRecalled) display.Add(m);
                }
            }
            return display;
        }

        // Retrieves Sent mails in recent-first order, excluding deleted
        static List<Mail> GetSentDisplayList(User u)
        {
            List<Mail> display = new List<Mail>();
            for (int i = u.SentIds.Count - 1; i >= 0; i--)
            {
                int mailId = u.SentIds[i];

                bool isDeleted = false;
                foreach (int delId in u.DeletedSentIds) if (delId == mailId) isDeleted = true;

                if (!isDeleted)
                {
                    Mail m = GetMailById(mailId);
                    if (m != null) display.Add(m);
                }
            }
            return display;
        }
    }
}