using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Npgsql;
using System.Text.RegularExpressions;
using YandexDiskNET;

internal class Program
{   
    static private TelegramBotClient client;
    static private NpgsqlConnection con;
    static string myConnectionString;
    static string myTokenString;
    static string yandextoken;
    static YandexDiskRest disk;
    static DiskInfo diskInfo;


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
        System.IO.File.AppendAllText("Config.cfg", "TelegramToken " + Linetext+"\n");
        Console.WriteLine("Enter Yandex token:");
        Linetext = Console.ReadLine();
        System.IO.File.AppendAllText("Config.cfg", "YandexToken " + Linetext + "\n"); 
        yandextoken = Linetext;
        Console.WriteLine("Enter host of database:");
        Linetext = Console.ReadLine();
        myConnectionString = "Host =" + Linetext+";";
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
        string datasours="", uid = "", database = "", password = "";
        string[] Mass = System.IO.File.ReadAllLines(@"Config.cfg", System.Text.Encoding.Default);
        for (int i = 0; i < Mass.Length; i++)
        {
            Console.WriteLine(Mass[i]);
            if((Mass[i].Split(" "))[0] == "TelegramToken")
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
                yandextoken= (Mass[i].Split(" "))[1];
            }
        }

        myConnectionString = "Host =" + datasours + ";Username=" + uid+ "; Password="+ password + ";Database=" + database;
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
   
    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {    
        if (update.Type != UpdateType.Message)
            return;
        // Only process text messages
        if (update.Message!.Type != MessageType.Text)
            return;

        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text;

        if (messageText != null)
        {
            if (messageText[0].ToString() == "/")
            {
                command(messageText, botClient, cancellationToken, chatId, update);
            }
            else
            {
                register(update,  botClient,  cancellationToken, chatId);
            }
        }
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
    static async Task command(string cmd, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId, Update update)
    {
        Console.WriteLine(cmd);
        switch (cmd)
        {
            case "/start":
                start(update, botClient, cancellationToken, chatId);
                break;
        }
    }
    static async void choicelab(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {
        try
        {
            string sql = "SELECT status FROM \"users\" WHERE chat_id = (" + update.Message.Chat.Id + ")";

            NpgsqlNestedDataReader reader1;
            NpgsqlDataReader reader;
            NpgsqlCommand command, command1;

            Console.WriteLine(sql);
            command = new NpgsqlCommand(sql, con);
            reader = command.ExecuteReader();

            if (reader.Read())
            {
                string status = reader["status"].ToString();
             
                if ((status.Split(" "))[0] == "choice")
                {
                    reader.Close();

                    if (((status.Split(" "))[1] == "1"))
                    {
                        sql = "SELECT need_var FROM \"labs_config\" WHERE id = (1) ;";
                        Console.WriteLine(sql);
                        command = new NpgsqlCommand(sql, con);
                        reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            int number = Int32.Parse(reader["need_var"].ToString());
                            reader.Close();
                            sql = "SELECT id,name_labs FROM \"listlabs\" WHERE c_labs = (1) ORDER BY id";
                            Console.WriteLine(sql);
                            command = new NpgsqlCommand(sql, con);
                            reader = command.ExecuteReader();

                            string text = "";

                            int neednumber = 0;
                            while (await reader.ReadAsync())
                            {
                                neednumber++;
                                Console.WriteLine(reader["name_labs"]);
                                text = text + neednumber + ") " + reader["name_labs"] + "\n";
                            }

                            reader.Close();
                            sql = String.Format("UPDATE \"users\" SET  status = 'choice 2' WHERE chat_id={0};", update.Message.Chat.Id);
                            Console.WriteLine(sql);
                            command1 = new NpgsqlCommand(sql, con);
                            command1.ExecuteNonQuery();

                            await botClient.SendTextMessageAsync(
                                  chatId: chatId,
                                 text: "Вам нужно выбрать " + number + " Лабораторных работ из списка:\n" + text + "\n Введите данные в формате N - Где N Это номер лабораторной работы ",
                                 cancellationToken: cancellationToken
                             );
                        }

                    }
                    else if (((status.Split(" "))[1] == "2"))
                    {
                        List<string> listSQL = new List<string>();
                        // Проверка данных
                        string[] numbersstr = ((update.Message.Text).ToString()).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Split(' ');
                        bool check = true;
                        if (numbersstr.Length >= 1)
                        {
                            if ((!Regex.IsMatch(update.Message.Text.ToString(), @"^[\d\s]+$")))
                            {
                                check = false;
                            }
                            for (int i = 1; (i < numbersstr.Length) && check; i++)
                            {

                                if (Int32.Parse(numbersstr[0]) == ( Int32.Parse(numbersstr[i])) || Int32.Parse(numbersstr[i])>=5 || Int32.Parse(numbersstr[0])>=5)
                                {
                                    check = false; break;
                                }
                            }
                            if (numbersstr.Length == 2  && check)
                            {
                                reader.Close();
                                sql = "SELECT id,name_labs,now_var,max_var FROM \"listlabs\" WHERE c_labs = (1) ORDER BY id";
                                Console.WriteLine(sql);
                                command = new NpgsqlCommand(sql, con);
                                reader = command.ExecuteReader();
                                int neednumber = 1;
                                string text = "Вами были выбраны следующие работы:\n";
                                string finalsql="";
                                bool es = false;
                                string sql1 = "";
                                while (await reader.ReadAsync())
                                {
                               
                                    for (int i = 0  ; i < 2; i++)
                                    {

                                        if (neednumber.ToString() == numbersstr[i])
                                        {
                                            text = text + neednumber + ")" + reader["name_labs"] + "Ваш вариант " + reader["now_var"] + "\n";
                                            if (Convert.ToInt32(reader["now_var"]) + 1 < Convert.ToInt32(reader["max_var"]))
                                            {
                                                sql1 = String.Format("UPDATE \"listlabs\" SET  now_var = now_var+1 WHERE name_labs=\'{0}\'", reader["name_labs"].ToString());
                                                listSQL.Add(sql1);
                                            }
                                            else
                                            {
                                                sql1 = String.Format("UPDATE \"listlabs\" SET  now_var = (1) WHERE name_labs=\'{0}\'", reader["name_labs"].ToString());
                                                listSQL.Add(sql1);
                                            }
                                      
                                        }
                                    }

                                    neednumber++;
                                }
                                Console.WriteLine("Output SQL:" + finalsql);
                                command1 = new NpgsqlCommand(finalsql, con);
                                command1.ConfigureAwait(false);
                                command1.ExecuteNonQueryAsync();
                                await botClient.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: text,
                                    cancellationToken: cancellationToken
                                );
                                reader.Close();
                                for(int indexer = 0; indexer < listSQL.Count; indexer++)
                                {
                                    command1 = new NpgsqlCommand(listSQL[indexer], con);
                                    command1.ExecuteNonQuery();
                                }
                                
                                Console.WriteLine(sql);
                                command1 = new NpgsqlCommand(sql, con);
                                command1.ExecuteNonQuery();
                                sql = String.Format("UPDATE \"users\" SET  labconfig1 = \'{0}\', status = 'choice 3'  WHERE chat_id={1};", text, update.Message.Chat.Id);
                                Console.WriteLine(sql);
                                command1 = new NpgsqlCommand(sql, con);
                                command1.ExecuteNonQuery();
                                choicelab(update, botClient, cancellationToken, chatId);
                            }

                            else
                            {
                                await botClient.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: "Что то пошло не так проверьте формат",
                                    cancellationToken: cancellationToken
                                );
                            }
                        }

                        if (!check)
                        {
                           await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: "Некоректные данные",
                                 cancellationToken: cancellationToken
                           );
                        }

                    }
                    else if (((status.Split(" "))[1] == "3"))
                    {
                        sql = "SELECT need_var FROM \"labs_config\" WHERE id = (2)";
                        Console.WriteLine(sql);
                        command = new NpgsqlCommand(sql, con);
                        reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            int number = Int32.Parse(reader["need_var"].ToString());
                            reader.Close();
                            sql = "SELECT id,name_labs FROM \"listlabs\" WHERE c_labs = (2) ORDER BY id";
                            Console.WriteLine(sql);
                            command = new NpgsqlCommand(sql, con);
                            reader = command.ExecuteReader();

                            string text = "";

                            {
                                int neednumber = 0;
                                while (await reader.ReadAsync())
                                {
                                    neednumber++;
                                    Console.WriteLine(reader["name_labs"]);
                                    text = text + neednumber + ") " + reader["name_labs"] + "\n";

                                }


                                reader.Close();
                                sql = String.Format("UPDATE \"users\" SET  status = 'choice 4' WHERE chat_id={0};", update.Message.Chat.Id);
                                command = new NpgsqlCommand(sql, con);
                                reader = command.ExecuteReader();
                                await botClient.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: "Вам нужно выбрать " + number + " Лабораторных работ из списка:\n" + text + "\n Введите данные в формате N N - Где N Это номер лабораторной работы ",
                                    cancellationToken: cancellationToken
                                );

                            }

                        }

                    }
                    else if (((status.Split(" "))[1] == "4"))
                    {
                        List<string> listSQL = new List<string>();
                        string[] numbersstr = ((update.Message.Text).ToString()).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Split(' ');
                        bool check = true;

                        if (numbersstr.Length >= 5)
                        {
                            if ((!Regex.IsMatch(update.Message.Text.ToString(), @"^[\d\s]+$")))
                            {
                                check = false;
                            }
                            
                            for (int i = 0; i < numbersstr.Length && check; i++)
                            {
                                for (int x = i + 1; x < numbersstr.Length;x++)
                                {
                                    if (Int32.Parse(numbersstr[i]) == Int32.Parse(numbersstr[x]) || Int32.Parse(numbersstr[x]) >= 15 || Int32.Parse(numbersstr[i]) >= 15)
                                    {
                                        check = false; 
                                        break;
                                    }

                                    if (!check) break;
                                }
                                
                            }

                            if (numbersstr.Length == 5  && check)
                            {
                                reader.Close();
                                sql = "SELECT id,name_labs,now_var,max_var,var_text,now_var_text FROM \"listlabs\" WHERE c_labs = (2) ORDER BY id";
                                Console.WriteLine(sql);
                                command = new NpgsqlCommand(sql, con);
                                
                                reader = command.ExecuteReader();
                                int neednumber = 1;
                                bool var_text = false;
                                string sql1 = "";
                                string text = "Вами были выбраны следующие работы:\n";
                                while (await reader.ReadAsync())
                                {
                                   
                                    for (int i = 0; i < 5; i++)
                                    {
                                       
                                        if (neednumber.ToString() == numbersstr[i])
                                        {

                                            string[] vartext = { "а", "б", "в", "г", "д" };
                                            
                                            if (reader["var_text"].ToString() == "True")
                                            {
                                                var_text = true;
                                            }
                                            else
                                            {
                                                var_text = false;
                                            }
                                            if(var_text)
                                            {
                                                text = text + neednumber + ")" + reader["name_labs"] + " Ваш вариант " + reader["now_var"] + reader["now_var_text"].ToString() + "\n";
                                            }
                                            else
                                            {
                                                text = text + neednumber + ")" + reader["name_labs"] + " Ваш вариант " + reader["now_var"] + "\n";
                                            }
                                            
                                            if(var_text)
                                            {
                                                int variant_now = 0;
                                                for (int s=0;s<5;s++)
                                                {
                                                    
                                                    if (vartext[s]== reader["now_var_text"].ToString())
                                                    { break; }
                                                    variant_now++;
                                                }
                                                if (variant_now < 4)
                                                {
                                                    sql = String.Format("UPDATE \"listlabs\" SET  now_var_text = \'{1}\' WHERE name_labs=\'{0}\'", reader["name_labs"].ToString(), vartext[variant_now+1]);
                                                    
                                                    listSQL.Add(sql);
                                                }
                                                else
                                                {
                                                    if (Convert.ToInt32(reader["now_var"]) + 1 < Convert.ToInt32(reader["max_var"]))
                                                    {
                                                        sql = String.Format("UPDATE \"listlabs\" SET  now_var = now_var+1, now_var_text='a' WHERE name_labs=\'{0}\'", reader["name_labs"].ToString());
                                                        listSQL.Add(sql);
                                                    }
                                                    else
                                                    {
                                                        sql = String.Format("UPDATE \"listlabs\" SET  now_var = (1), now_var_text='a' WHERE name_labs=\'{0}\'", reader["name_labs"].ToString());
                                                        listSQL.Add(sql);
                                                    }

                                                }
                                            }
                                            else
                                            {
                                                if (Convert.ToInt32(reader["now_var"]) + 1 < Convert.ToInt32(reader["max_var"]))
                                                {
                                                    sql = String.Format("UPDATE \"listlabs\" SET  now_var = now_var+1 WHERE name_labs=\'{0}\'", reader["name_labs"].ToString());
                                                    listSQL.Add(sql);
                                                }
                                                else
                                                {
                                                    sql = String.Format("UPDATE \"listlabs\" SET  now_var = (1) WHERE name_labs=\'{0}\'", reader["name_labs"].ToString());
                                                    listSQL.Add(sql);
                                                }
                                            }

                                        }
                                    }

                                    neednumber++;
                                }
                                Message sentMessage = await botClient.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: text,
                                    cancellationToken: cancellationToken
                                );
                                reader.Close();
                                for (int indexer = 0; indexer < listSQL.Count; indexer++)
                                {
                                    Console.WriteLine(listSQL[indexer]);
                                    command1 = new NpgsqlCommand(listSQL[indexer], con);
                                    command1.ExecuteNonQuery();
                                }
                                Console.WriteLine(sql);
                                command1 = new NpgsqlCommand(sql, con);
                                command1.ExecuteNonQuery();
                                sql = String.Format("UPDATE \"users\" SET  labconfig2 = \'{0}\', status = 'Final'  WHERE chat_id={1};", text, update.Message.Chat.Id);
                                Console.WriteLine(sql);
                                command1 = new NpgsqlCommand(sql, con);
                                command1.ExecuteNonQuery();
                                reader.Close();
                                sql = String.Format("SELECT u_name,group_n,labconfig1,labconfig2,telegram_name  FROM \"users\" ");
                                SqlToFile(sql, "Users.csv");
                            }
                            else
                            {
                               await botClient.SendTextMessageAsync(
                                   chatId: chatId,
                                   text: "Что то пошло не так проверьте формат",
                                   cancellationToken: cancellationToken
                              );
                            }
                        }
                        else
                        {
                            check = false;
                        }

                        if(!check)
                        {
                            await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: "Что то пошло не так проверьте формат",
                                 cancellationToken: cancellationToken
                            );
                        }

                    }
                }
            }
            reader.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error " + ex.Message);
            throw new Exception(ex.Message, ex);
        }
    }
    static async void finalall(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {
        string sql;
        NpgsqlDataReader reader;
        NpgsqlCommand command, command1;
        sql = "SELECT labconfig1,labconfig2 FROM \"users\" WHERE chat_id = (" + update.Message.Chat.Id + ")";
        command = new NpgsqlCommand(sql, con);
        reader = command.ExecuteReader();
        string text1, text2;
        
        if (reader.Read())
        {
            text1 = reader["labconfig1"].ToString();
            text2 = reader["labconfig2"].ToString();
            reader.Close();
            
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text1+"\n",
                cancellationToken: cancellationToken
            );

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text2 + "\n",
                cancellationToken: cancellationToken
             );
        }
    }
    static async void register(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {
        try
        {
            string sql = "SELECT status FROM \"users\" WHERE chat_id = (" + update.Message.Chat.Id + ")";
            NpgsqlDataReader reader;
            NpgsqlCommand command, command1;

            Console.WriteLine(sql);

            command = new NpgsqlCommand(sql, con);
            reader = command.ExecuteReader();

            if(reader.Read())
            {
                string status = reader["status"].ToString();
                reader.Close();

                if ((status.Split(" "))[0]=="register")
                {
                    if ((status.Split(" "))[1] == "1")
                    {
                        reader.Close();
                        sql = String.Format("UPDATE \"users\" SET  status = 'register 2' WHERE chat_id={1};", update.Message.Text, update.Message.Chat.Id);
                        Console.WriteLine(sql);
                        command1 = new NpgsqlCommand(sql, con);
                        command1.ExecuteNonQuery();
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Введите ваше ФИО:\n",
                            cancellationToken: cancellationToken
                        );
                        Console.WriteLine("Step 1 of registration : ");
                    }
                    else if((status.Split(" "))[1] == "2")
                    {
                        reader.Close();
                        sql = String.Format("UPDATE \"users\" SET u_name = \'{0}\', status = 'register 3' WHERE chat_id={1};", update.Message.Text, update.Message.Chat.Id);
                        Console.WriteLine(sql);
                        command1 = new NpgsqlCommand(sql, con);
                        command1.ExecuteNonQuery();
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Введите номер группы:\n",
                            cancellationToken: cancellationToken
                        );
                        Console.WriteLine("Step 2 of registration : ");
                    }
                    else if ((status.Split(" "))[1] == "3")
                    {
                        reader.Close();
                        sql = String.Format("UPDATE \"users\" SET group_n = \'{0}\', status = 'choice 1' WHERE chat_id={1};", update.Message.Text, update.Message.Chat.Id);
                        Console.WriteLine(sql);
                        command1 = new NpgsqlCommand(sql, con);
                        command1.ExecuteNonQuery();
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Вы успешно зарегистрировались, пора выбрать работы:\n",
                            cancellationToken: cancellationToken
                        );
                        Console.WriteLine("Registration completed : ");
                        choicelab(update, botClient, cancellationToken, chatId);
                    }
                    else
                    {
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Я не умею работать с такими запросами:\n",
                            cancellationToken: cancellationToken
                         );
                    }
                    
                }
                else if ((status.Split(" "))[0] == "choice")
                {
                    reader.Close();
                    choicelab(update, botClient, cancellationToken, chatId);
                }
                else if ((status.Split(" "))[0] == "Final")
                {
                    reader.Close();
                    finalall(update, botClient, cancellationToken, chatId);
                }
            }
            reader.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Registration error: " + ex);
        }
    }
    static async void start(Update update, ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId)
    {
        try
        {
            string sql = "SELECT chat_id FROM \"users\" WHERE chat_id = (" + update.Message.Chat.Id + ")";
            NpgsqlDataReader reader;
            NpgsqlCommand command, command1;

            Console.WriteLine(sql);

            command = new NpgsqlCommand(sql, con);
            reader = command.ExecuteReader();

            if (!reader.Read())
            {
                try
                {
                    reader.Close();
                    sql = String.Format("INSERT INTO \"users\" (chat_id,u_name,group_n,status,telegram_name) VALUES ({0},'none','none','register 1','{1}')", update.Message.Chat.Id, update.Message.Chat.Username);
                    Console.WriteLine(sql);
                    command1 = new NpgsqlCommand(sql, con);
                    command1.ExecuteNonQuery();

                    Console.WriteLine("New user");
                    Message sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Данный бот используется для получения вариантов на лабораторные \r\nработы по курсу “Информационная Безопасность”.:\n",
                        cancellationToken: cancellationToken
                    );
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
            reader.Close();
            sql = String.Format("SELECT u_name,group_n,labconfig1,labconfig2,telegram_name,chat_id  FROM \"users\" ");
            SqlToFile(sql, "Users.csv");
            register(update, botClient, cancellationToken, chatId);             

        }
        catch (Exception ex)
        {
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

    static async void SqlToFile(string sql, string FileName)
    {
        NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
        NpgsqlDataReader queryReader = cmd.ExecuteReader();
        StreamWriter file = new StreamWriter(FileName, false, Encoding.Unicode);

        string headertext = "ФИО \t Ник телеграм \t Группа \t Лаб1 \t Лаб2 \t";

        file.WriteLine(headertext);

        while (queryReader.Read()) 
        {
            string text = queryReader["u_name"].ToString() 
                + "\t" + " " + queryReader["telegram_name"].ToString()
                + "\t" + queryReader["group_n"].ToString()
                + "\t" + ((queryReader["labconfig1"].ToString()).Replace("\n", " ")).Replace("Вами были выбраны следующие работы:", "") 
                + "\t" + ((queryReader["labconfig2"].ToString()).Replace("\n", " ")).Replace("Вами были выбраны следующие работы:", "");

            text.Replace("\r\n", "");
            text.Replace("\r", "");
            text.Replace("\n", "");
            file.WriteLine(text);
        }

        queryReader.Close();
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


