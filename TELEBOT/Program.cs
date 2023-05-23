using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Npgsql;
using YandexDiskNET;
using Telegram.Bot.Types.ReplyMarkups;
internal enum UserStatus
{ 
    AWAITING_NAME,
    AWAITING_GROUP,
    AWAITING_CONFIG,
    AWAITING_AGREE,
    FINAL
}

internal class Lab
{ 
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Name { get; set; }
    public int MaxVar { get; set; }
    public int CurrVar { get; set; }
    public bool HasTextVar { get; set; }
    public string TextVar { get; set; }
}

internal class LabVariant
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Variant { get; set; }
}

internal class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Group { get; set; }
    public List<LabVariant> Config { get; set; }
    public long ChatId { get; set; }
    public int LabGroupId { get; set; }
    public int LabsCount { get; set; }
    public string TgName { get; set; }
    public UserStatus Status { get; set; }
}

internal class Program
{
    static string myConnectionString;
    static string myTokenString;
    static string yandextoken;
    static YandexDiskRest disk;
    static DiskInfo diskInfo;
    static TelegramBotClient client;
    static NpgsqlConnection con;
    static Mutex mutex = new Mutex();

    static string toString(UserStatus status)
    {
        switch (status)
        {
            case UserStatus.AWAITING_NAME:
                return "AWAITING_NAME";
            case UserStatus.AWAITING_GROUP:
                return "AWAITING_GROUP";
            case UserStatus.AWAITING_CONFIG:
                return "AWAITING_CONFIG";
            case UserStatus.AWAITING_AGREE:
                return "AWAITING_AGREE";
            case UserStatus.FINAL:
                return "FINAL";
            default:
                return "UNKNOWN";
        }
    }

    static UserStatus fromString(string data)
    {
        switch (data)
        {
            case "AWAITING_NAME":
                return UserStatus.AWAITING_NAME;
            case "AWAITING_GROUP":
                return UserStatus.AWAITING_GROUP;
            case "AWAITING_CONFIG":
                return UserStatus.AWAITING_CONFIG;
            case "AWAITING_AGREE":
                return UserStatus.AWAITING_AGREE;
            case "FINAL":
                return UserStatus.FINAL;
            default:
                return UserStatus.FINAL;
        }
    }

    static bool ConnectToYandexDisk()
    {
        disk = new YandexDiskRest(yandextoken);
        diskInfo = disk.GetDiskInfo();

        DiskInfo diskInfoFeilds = disk.GetDiskInfo(new DiskFields[] {
                DiskFields.Total_space,
                DiskFields.Used_space,
                DiskFields.User
        });

        Console.WriteLine(diskInfoFeilds);

        if (diskInfoFeilds.ErrorResponse.Message == null)
        {
            Console.WriteLine("User: {0}\nTotal: size {1} bytes\nUsed: {2} bytes", diskInfoFeilds.User.Login, diskInfoFeilds.Total_space, diskInfoFeilds.Used_space);
            return true;
        }
        else
        {
            Console.WriteLine("No connect to disk");
            Console.WriteLine(diskInfoFeilds.ErrorResponse.Message);
            return false;
        }
    }

    static void newcfg()
    {
        string Linetext;
        Console.WriteLine("Enter telegram token:");
        Linetext = Console.ReadLine();
        myTokenString = Linetext;
        System.IO.File.AppendAllText("Config.cfg", "TelegramToken " + Linetext + "\n");
        Console.WriteLine("Enter Yandex token:");
        Linetext = Console.ReadLine();
        System.IO.File.AppendAllText("Config.cfg", "YandexToken " + Linetext + "\n");
        yandextoken = Linetext;
        Console.WriteLine("Enter host of database:");
        Linetext = Console.ReadLine();
        myConnectionString = "Host =" + Linetext + ";";
        System.IO.File.AppendAllText("Config.cfg", "DataSource " + Linetext + "\n");
        Console.WriteLine("Enter username of database:");
        Linetext = Console.ReadLine();
        myConnectionString = myConnectionString + "Username=" + Linetext + ";";
        System.IO.File.AppendAllText("Config.cfg", "uid " + Linetext + "\n");
        Console.WriteLine("Enter password:");
        Linetext = Console.ReadLine();
        myConnectionString = myConnectionString + "Password=" + Linetext + ";";
        System.IO.File.AppendAllText("Config.cfg", "password " + Linetext + "\n");
        Console.WriteLine("Enter name of database");
        Linetext = Console.ReadLine();
        myConnectionString = myConnectionString + "Database=" + Linetext + ";";
        System.IO.File.AppendAllText("Config.cfg", "database " + Linetext + "\n");
    }


    static void Configload()
    {
        string datasours = "", uid = "", database = "", password = "";
        string[] Mass = System.IO.File.ReadAllLines(@"Config.cfg", System.Text.Encoding.Default);
        for (int i = 0; i < Mass.Length; i++)
        {
            Console.WriteLine(Mass[i]);
            if ((Mass[i].Split(" "))[0] == "TelegramToken")
            {
                myTokenString = (Mass[i].Split(" "))[1];
            }
            if ((Mass[i].Split(" "))[0] == "DataSource")
            {
                datasours = (Mass[i].Split(" "))[1];
            }

            if ((Mass[i].Split(" "))[0] == "uid")
            {
                uid = (Mass[i].Split(" "))[1];
            }
            if ((Mass[i].Split(" "))[0] == "database")
            {
                database = (Mass[i].Split(" "))[1];
            }
            if ((Mass[i].Split(" "))[0] == "password")
            {
                password = (Mass[i].Split(" "))[1];
            }
            if ((Mass[i].Split(" "))[0] == "YandexToken")
            {
                yandextoken = (Mass[i].Split(" "))[1];
            }
        }

        myConnectionString = "Host =" + datasours + ";Username=" + uid + "; Password=" + password + ";Database=" + database;
    }

    static bool configcheck()
    {
        if (System.IO.File.Exists("Config.cfg"))
        {
            Console.WriteLine("Config found");
            return true;
        }
        else
        {
            Console.WriteLine("Config not found");
            return false;
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
    static async Task Connect()
    {
        var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } // receive all update types
        };
        client = new TelegramBotClient(myTokenString);

        client.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token
        );

        var me = await client.GetMeAsync();

        Console.WriteLine($"Connected to @{me.Username}");
        Console.WriteLine($"Name {me.Id} Name {me.FirstName}.");

        Console.ReadLine();
        cts.Cancel();
    }

    static bool ConnectToTelegram()
    {
        try
        {
            Console.WriteLine("Connection to TelegramAPI");
            var task = Connect();

            task.Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[API] Connection error to TelegramAPI " + ex.Message);
        }
        return false;
    }

    static List<User> GetAllUsers()
    {
        List<User> users = new List<User>();

        mutex.WaitOne();

        string sql = "SELECT * FROM \"users\"";
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        NpgsqlDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            User user = new User();

            user.ChatId = Int32.Parse(reader["chat_id"].ToString());

            user.Id = Int32.Parse(reader["id"].ToString());
            user.LabsCount = Int32.Parse(reader["labs_count"].ToString());
            user.LabGroupId = Int32.Parse(reader["lab_group"].ToString());
            user.Group = reader["group_n"].ToString();
            user.Name = reader["u_name"].ToString();
            user.Status = fromString(reader["status"].ToString());
            user.TgName = reader["telegram_name"].ToString();

            string config = reader["config"].ToString();
            string[] array = config.Split(';');
            List<LabVariant> vars = new List<LabVariant>();

            for (int i = 0; i + 2 < array.Length; i += 3)
            {
                int groupId = Int32.Parse(array[i]);
                int labId = Int32.Parse(array[i + 1]);
                string variant = array[i + 2];

                LabVariant lab = new LabVariant();

                lab.GroupId = groupId;
                lab.Id = labId;
                lab.Variant = variant;

                vars.Add(lab);
            }

            user.Config = vars;

            users.Add(user);
        }

        mutex.ReleaseMutex();

        reader.Close();
        return users;
    }

    static User GetUserByChatId(long chatId)
    {
        mutex.WaitOne();

        string sql = "SELECT * FROM \"users\" WHERE chat_id = (" + chatId + ")";
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        NpgsqlDataReader reader = cmd.ExecuteReader();

        if (reader.Read())
        { 
            User user = new User();

            user.ChatId = chatId;

            user.Id = Int32.Parse(reader["id"].ToString());
            user.LabsCount = Int32.Parse(reader["labs_count"].ToString());
            user.LabGroupId = Int32.Parse(reader["lab_group"].ToString());
            user.Group = reader["group_n"].ToString();
            user.Name = reader["u_name"].ToString();
            user.Status = fromString(reader["status"].ToString());
            user.TgName = reader["telegram_name"].ToString();

            string config = reader["config"].ToString();
            string[] array = config.Split(';');
            List<LabVariant> vars = new List<LabVariant>();

            for (int i = 0; i + 2 < array.Length; i += 3)
            {
                int groupId = Int32.Parse(array[i]);
                int labId = Int32.Parse(array[i + 1]);
                string variant = array[i + 2];

                LabVariant lab = new LabVariant();

                lab.GroupId = groupId;
                lab.Id = labId;
                lab.Variant = variant;

                vars.Add(lab);
            }

            user.Config = vars;

            mutex.ReleaseMutex();
            reader.Close();
            return user;
        }

        mutex.ReleaseMutex();
        reader.Close();
        return null;
    }

    static SortedDictionary<int, int> GetLabConfig()
    {
        SortedDictionary<int, int> config = new SortedDictionary<int, int>();

        mutex.WaitOne();

        string sql = "SELECT id,need_var FROM \"labs_config\" ORDER BY id";
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        NpgsqlDataReader reader = cmd.ExecuteReader();

        while (reader.Read()) 
        {
            int groupId = Int32.Parse(reader["id"].ToString());
            int amount = Int32.Parse(reader["need_var"].ToString());

            config.Add(groupId, amount);
        }

        mutex.ReleaseMutex();
        reader.Close();
        return config;
    }

    static List<string> GetGroups()
    {
        List<string> groups = new List<string>();

        mutex.WaitOne();

        string sql = "SELECT id,g_name FROM \"g_list\" ORDER BY id";
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        NpgsqlDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            groups.Add(reader["g_name"].ToString());
        }

        mutex.ReleaseMutex();
        reader.Close();
        return groups;
    }

    static List<Lab> GetLabs()
    {
        List<Lab> labs = new List<Lab>();

        mutex.WaitOne();

        string sql = "SELECT * FROM \"listlabs\" ORDER BY id";
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        NpgsqlDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            int id = Int32.Parse(reader["id"].ToString());
            int groupId = Int32.Parse(reader["c_labs"].ToString());
            string name = reader["name_labs"].ToString();
            int max_var = Int32.Parse(reader["max_var"].ToString());
            int curr_var = Int32.Parse(reader["now_var"].ToString());
            bool is_text = (reader["var_text"].ToString() == "True");
          
            Lab lab = new Lab();

            lab.Id = id;
            lab.GroupId = groupId;
            lab.Name = name;
            lab.MaxVar = max_var;
            lab.CurrVar = curr_var;
            lab.HasTextVar = is_text;

            if (lab.HasTextVar && reader["now_var_text"] != null) 
            {
                lab.TextVar = reader["now_var_text"].ToString();
            }

            labs.Add(lab);
        }

        mutex.ReleaseMutex();
        reader.Close();
        return labs;
    }

    static void updateUser(long chatId, User user)
    {
        string config = "";

        foreach(var var in user.Config)
        {
            config += var.GroupId + ";" + var.Id + ";" + var.Variant + ";";
        }

        if (config.Length != 0)
        {
            config = config.Substring(0, config.Length - 1);
        }

        mutex.WaitOne();

        string sql = String.Format("UPDATE \"users\" SET u_name = '{0}', group_n = '{1}', status = '{2}', config = '{3}', lab_group = {4}, labs_count={5}  WHERE chat_id = {6}",
                    user.Name, user.Group, toString(user.Status), config, user.LabGroupId, user.LabsCount, chatId);
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        cmd.ExecuteNonQuery();

        mutex.ReleaseMutex();
    }

    static string GetNextVarAndUpdate(Lab lab)
    {
        string var = lab.HasTextVar ? lab.CurrVar.ToString() + lab.TextVar : lab.CurrVar.ToString();

        if (lab.HasTextVar)
        {
            int i = 0;
            string[] data = { "а", "б", "в", "г", "д" };

            for (; i != data.Length; i++)
            {
                if (data[i].Equals(lab.TextVar))
                {
                    break;              
                }
            }

            if (i != data.Length - 1)
            {
                lab.TextVar = data[i + 1];
            }
            else
            {
                lab.TextVar = data[0];
                lab.CurrVar += 1;

                if (lab.CurrVar > lab.MaxVar)
                {
                    lab.CurrVar = 1;
                }
            }
        }
        else
        {
            lab.CurrVar += 1;

            if (lab.CurrVar > lab.MaxVar)
            {
                lab.CurrVar = 1;
            }
        }

        mutex.WaitOne();

        string sql = String.Format("UPDATE \"listlabs\" SET now_var = {0} {1} WHERE id = {2}", lab.CurrVar, lab.HasTextVar ? String.Format(", now_var_text = '{0}'", lab.TextVar) : "", lab.Id);
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        cmd.ExecuteNonQuery();

        mutex.ReleaseMutex();

        return var;
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message)
        {
            if (update.Message == null || update.Message.Text == null) return;

            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;

            if (messageText[0].ToString() == "/")
            {
                await command(messageText, botClient, cancellationToken, chatId, update);
            }
            else
            {
                await textMsg(messageText, botClient, cancellationToken, chatId, update); 
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            if (update.CallbackQuery == null || update.CallbackQuery.Message == null || update.CallbackQuery.Data == null) return;

            var msgId = update.CallbackQuery.Message.MessageId;
            var chatId = update.CallbackQuery.Message.Chat.Id;
            var callbackData = update.CallbackQuery.Data;

            await buttonCallback(callbackData, botClient, cancellationToken, chatId, msgId, update);
        }
    }
    static async Task buttonCallback(string data, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId, int msgId, Update update)
    {
        User user = GetUserByChatId(chatId);
        SortedDictionary<int, int> labConfig = GetLabConfig();
        List<Lab> labs = GetLabs();

        if (user == null) return;

        if (user.Status != UserStatus.AWAITING_GROUP && user.Status != UserStatus.AWAITING_CONFIG) return;

        if (user.Status == UserStatus.AWAITING_GROUP)
        {
            user.Status = UserStatus.AWAITING_CONFIG;
            user.Group = data;


            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Вы уcпешно зарегистрировались:\r\nФИО - " + user.Name + "\r\n" + "Группа - " + user.Group 
                + "\r\nДля выбора лабораторных работ введите /choose",
                cancellationToken: cancellationToken
            );
        }
        else if (user.Status == UserStatus.AWAITING_CONFIG)
        {
            string[] array = data.Split(';');

            if (array.Length != 2) return;

            int groupId = Int32.Parse(array[0]);
            int labId = Int32.Parse(array[1]);

            // If old button then ignore
            if (groupId != user.LabGroupId) return;

            // if lab already was chosen
            if (user.Config.Any(var => var.Id == labId)) return;

            int index = labs.FindIndex(lab => lab.Id == labId);

            if (index == -1) return;

            Lab lab = labs[index];

            LabVariant labVar = new LabVariant();

            labVar.Id = labId;
            labVar.GroupId = groupId;
            labVar.Variant = GetNextVarAndUpdate(lab);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: lab.Name + " - вариант " + labVar.Variant,
                cancellationToken: cancellationToken
            );

            user.LabsCount += 1;
            user.Config.Add(labVar);

            int amount = labConfig.GetValueOrDefault(groupId);

            if (amount == user.LabsCount)
            {
                user.LabsCount = 0;
                user.LabGroupId += 1;

                if (!labConfig.ContainsKey(user.LabGroupId))
                {
                    user.Status = UserStatus.FINAL;
                    user.LabGroupId = 0;
                    user.LabsCount = 0;
                }
            }

            // if first lab in group
            if (user.LabGroupId != 0 && user.LabsCount == 0)
            {
                var list = new List<InlineKeyboardButton[]>();

                foreach (var current in labs)
                {
                    if (current.GroupId == user.LabGroupId)
                    {
                        var button = InlineKeyboardButton.WithCallbackData(current.Name, current.GroupId.ToString() + ";" + current.Id);
                        var row = new InlineKeyboardButton[1] { button };

                        list.Add(row);
                    }
                }

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Выберите лабораторные работы? (Всего " + labConfig.GetValueOrDefault(user.LabGroupId) + ")",
                    replyMarkup: new InlineKeyboardMarkup(list),
                    cancellationToken: cancellationToken
                );
            }
        }

        if (user.Status == UserStatus.FINAL)
        {
            string result = "Выбор лабораторных завершен:\r\n";

            foreach (var conf in user.Config)
            {
                int index = labs.FindIndex(lab => lab.Id == conf.Id);

                if (index == -1) continue;

                result += labs[index].Name + " - вариант " + conf.Variant + "\r\n";
            }

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: result,
                cancellationToken: cancellationToken
            );
        }

        updateUser(chatId, user);

        if (user.Status == UserStatus.FINAL)
        {
            UploadFile("Users.csv");
        }
    }

    static async Task command(string cmd, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId, Update update)
    {
        Console.WriteLine(cmd);
        switch (cmd)
        {
            case "/start":
                start(update, botClient, cancellationToken, chatId);
                break;
            case "/info":
                info(update, botClient, cancellationToken, chatId);
                break;
            case "/choose":
                choose(update, botClient, cancellationToken, chatId);
                break;
            case "/help":
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Это иформация по боту\n /info - Вывести информацию \n /choose - приступить к выбору лабораторных работ",
                    cancellationToken: cancellationToken
                );
                break;
            default:
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Неизвестная команда:\r\n/start - регистрация\r\n/info - вывод информации\r\n/choose - выбор лабораторных работ\n",
                    cancellationToken: cancellationToken
                );
                break;
        }
    }

    static async Task textMsg(string text, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId, Update update)
    {
        User user = GetUserByChatId(chatId);
        List<string> groups = GetGroups();

        if (user == null) return;

        if (user.Status == UserStatus.AWAITING_NAME)
        {
            user.Status = UserStatus.AWAITING_GROUP;
            user.Name = text;

            var list = new List<InlineKeyboardButton[]>();

            foreach (var group in groups)
            {
                var button = InlineKeyboardButton.WithCallbackData(group);
                var row = new InlineKeyboardButton[1] { button };

                list.Add(row);
            }

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Выберите группу:",
                replyMarkup: new InlineKeyboardMarkup(list),
                cancellationToken: cancellationToken
            );

            updateUser(chatId, user);
        }
    }

    static async void info(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {
        await botClient.SendTextMessageAsync(
             chatId: chatId,
             text: "Данный бот используется для получения вариантов на лабораторные \r\nработы по курсу “Информационная Безопасность”.:\n",
             cancellationToken: cancellationToken
        );
    }

    static async void choose(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {
        User user = GetUserByChatId(chatId);
        SortedDictionary<int, int> labConfig = GetLabConfig();
        List<Lab> labs = GetLabs();

        if (user == null) return;

        if (user.Status == UserStatus.FINAL)
        {
            string result = "";

            foreach (var labVar in user.Config)
            {
                int index = labs.FindIndex(lab => lab.Id == labVar.Id);

                if (index == -1) continue;

                result += labs[index].Name + " - вариант " + labVar.Variant + "\r\n";
            }

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: result,
                cancellationToken: cancellationToken
            );

            return;
        }

        if (user.Status != UserStatus.AWAITING_CONFIG) 
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Вы еще не зарегистрировались в боте или не ввели ФИО/группу",
                cancellationToken: cancellationToken
            );

            return;
        }

        if (user.Config.Count > 0)
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Вы уже начали выбор лабораторных работ",
                cancellationToken: cancellationToken
            );

            return;
        }
        // Only first group
        user.LabGroupId = 1;
        user.LabsCount = 0;

        var list = new List<InlineKeyboardButton[]>();

        foreach (var current in labs)
        {
            if (user.LabGroupId == current.GroupId)
            {
                var button = InlineKeyboardButton.WithCallbackData(current.Name, current.GroupId.ToString() + ";" + current.Id);
                var row = new InlineKeyboardButton[1] { button };

                list.Add(row);
            }
        }

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите лабораторные работы? (Всего " + labConfig.GetValueOrDefault(user.LabGroupId) + ")",
            replyMarkup: new InlineKeyboardMarkup(list),
            cancellationToken: cancellationToken
        );

        updateUser(chatId, user);
    }

    static async void start(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {


        try
        {
            mutex.WaitOne();

            string sql = "SELECT chat_id FROM \"users\" WHERE chat_id = (" + update.Message.Chat.Id + ")";
            NpgsqlCommand command, command1;
            NpgsqlDataReader reader;

            Console.WriteLine(sql);


            command = new NpgsqlCommand(sql, con);
            reader = command.ExecuteReader();

            if (!reader.Read())
            {
                try
                {
                    reader.Close();
                    sql = String.Format("INSERT INTO \"users\" (chat_id,u_name,group_n,status,telegram_name,config,lab_group, labs_count) VALUES ({0},'none','none','{1}','{2}', '', 0, 0)", update.Message.Chat.Id, toString(UserStatus.AWAITING_NAME), update.Message.Chat.Username);
                    Console.WriteLine(sql);
                    command1 = new NpgsqlCommand(sql, con);
                    command1.ExecuteNonQuery();

                    Console.WriteLine("New user");
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Данный бот используется для получения вариантов на лабораторные \r\nработы по курсу “Информационная Безопасность”.:\n",
                        cancellationToken: cancellationToken
                    );

                    await botClient.SendTextMessageAsync(
                       chatId: chatId,
                       text: "Введите ФИО:\n",
                       cancellationToken: cancellationToken
                   );

                    UploadFile("Users.csv");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Registration error: " + ex);
                }
            }
            else
            {
                Console.WriteLine("Already registered ");
                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Вы уже начали регистрацию или закончили ее",
                    cancellationToken: cancellationToken
                );
            }

            mutex.ReleaseMutex();
            reader.Close();
        }
        catch (Exception ex)
        {
            mutex.ReleaseMutex();
            Console.WriteLine(ex);
        }
    }
    static bool ConnectToDatabase()
    {
        try
        {
            Console.WriteLine("Connection to database");
            Console.WriteLine(myConnectionString);

            con = new NpgsqlConnection(myConnectionString);
            con.OpenAsync();
            Console.WriteLine("Connected to database");

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    static void UploadFile(string FileName)
    {
        SortedDictionary<int,int> labConfig = GetLabConfig();
        List<Lab> labs = GetLabs();
        List<User> users = GetAllUsers();

        StreamWriter file = new StreamWriter(FileName, false, Encoding.Unicode);

        string headertext = "ФИО \t Ник телеграм \t Группа \t ";

        foreach (var config in labConfig) 
        {
            for (int i = 0; i != config.Value; i++)
            {
                headertext += config.Key.ToString() + "," + (1 + i).ToString() + " \t";    
            }
        }

        file.WriteLine(headertext);

        foreach (User user in users)
        {
            string line = user.Name + " \t " + user.TgName + " \t " + user.Group + " \t ";

            foreach (var conf in user.Config)
            {
                int index = labs.FindIndex(lab => lab.Id == conf.Id);

                if (index != -1)
                {
                    Lab lab = labs[index];

                    line += lab.Name + " - вариант " + conf.Variant + " \t ";
                }
            }

            file.WriteLine(line);
        }

        file.Close();

        var err = disk.UploadResource(FileName, FileName, true);
        if (err.Message == null)
            Console.WriteLine("Uploaded successfully {0}", Path.GetFileName(FileName));
        else
            Console.WriteLine(err.Message);
    }

    private static void Main(string[] args)
    {
        if (!configcheck())
        {
            newcfg();
        }
        else
        {
            Configload();
        }

        if (!ConnectToYandexDisk()) return;
        if (!ConnectToDatabase()) return;
        if (!ConnectToTelegram()) return;
    }
}

