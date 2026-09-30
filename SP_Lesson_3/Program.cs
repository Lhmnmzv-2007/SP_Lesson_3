using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FileEncryptConsole
{
    class Program
    {
        static Thread workThread = null;
        static ManualResetEvent pauseEvent = new ManualResetEvent(true);
        static volatile bool isAborted = false;
        static volatile bool isWorking = false;

        static void Main(string[] args)
        {
            Console.WriteLine(" Fayl Encrypt / Decrypt (AES)\n");

            Console.Write("Fayl yolu : ");
            string filePath = Console.ReadLine()?.Trim('"');

            Console.Write("Password  : ");
            string password = Console.ReadLine();

            Console.Write("Emeliyyat (E - Encrypt / D - Decrypt): ");
            string op = Console.ReadLine()?.ToUpper();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("Fayl tapilmadi!");
                Console.ReadKey();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Password bos ola bilmez!");
                Console.ReadKey();
                return;
            }

            bool isEncrypt = op != "D";

            Console.WriteLine("\nEmrler:");
            Console.WriteLine("  C -> Baslat");
            Console.WriteLine("  S -> Suspend (dayandir)");
            Console.WriteLine("  R -> Resume  (davam et)");
            Console.WriteLine("  A -> Abort   (legv et)");
            Console.WriteLine("  Q -> Cixis\n");

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    switch (key)
                    {
                        case ConsoleKey.C:
                            if (!isWorking)
                            {
                                isAborted = false;
                                pauseEvent.Set();
                                isWorking = true;

                                workThread = new Thread(() => ProcessFile(filePath, password, isEncrypt));
                                workThread.IsBackground = true;
                                workThread.Start();

                                Console.WriteLine("\nIs basladi...");
                            }
                            else
                            {
                                Console.WriteLine("Artiq is gedir...");
                            }
                            break;

                        case ConsoleKey.S:
                            if (isWorking)
                            {
                                pauseEvent.Reset();
                                Console.WriteLine("\n Dayandirildi. Davam etmek ucun R basin.");
                            }
                            break;

                        case ConsoleKey.R:
                            if (isWorking)
                            {
                                pauseEvent.Set();
                                Console.WriteLine("\n Davam edir...");
                            }
                            break;

                        case ConsoleKey.A:
                            if (isWorking)
                            {
                                isAborted = true;
                                pauseEvent.Set();
                                Console.WriteLine("\n Legv emri gonderildi...");
                            }
                            break;

                        case ConsoleKey.Q:
                            if (isWorking)
                            {
                                isAborted = true;
                                pauseEvent.Set();
                                if (workThread != null && workThread.IsAlive)
                                    workThread.Join(1000);
                            }
                            return;
                    }
                }

                Thread.Sleep(40);
            }
        }

        static void ProcessFile(string filePath, string password, bool isEncrypt)
        {
            string tempPath = filePath + ".tmp";

            try
            {
                FileInfo fi = new FileInfo(filePath);
                long totalBytes = fi.Length;
                long processedBytes = 0;
                int bufferSize = 124;

                byte[] key = Encoding.UTF8.GetBytes(password.PadRight(32).Substring(0, 32));
                byte[] iv = new byte[16];

                using (FileStream sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (FileStream destStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    ICryptoTransform transform = isEncrypt
                        ? aes.CreateEncryptor()
                        : aes.CreateDecryptor();

                    using (CryptoStream cryptoStream = new CryptoStream(destStream, transform, CryptoStreamMode.Write))
                    {
                        byte[] buffer = new byte[bufferSize];
                        int bytesRead;

                        Console.WriteLine($"Umumi olcu: {totalBytes:N0} bayt\n");

                        while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (isAborted)
                                break;

                            pauseEvent.WaitOne();

                            if (isAborted)
                                break;

                            cryptoStream.Write(buffer, 0, bytesRead);
                            processedBytes += bytesRead;

                            double percent = (double)processedBytes / totalBytes * 100.0;
                            DrawProgressBar(percent, processedBytes, totalBytes);

                            Thread.Sleep(5);
                        }
                    }
                }

                if (isAborted)
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);

                    Console.WriteLine("\n\n Emeliyyat legv edildi ve yarimciq fayl silindi.");
                }
                else
                {
                    File.Delete(filePath);
                    File.Move(tempPath, filePath);

                    Console.WriteLine("\n\n[OK] Emeliyyat ugurla tamamlandi!");
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                Console.WriteLine($"\nXeta: {ex.Message}");
            }
            finally
            {
                isWorking = false;
                Console.WriteLine("\nYeniden baslamaq ucun C basin.");
            }
        }

        static void DrawProgressBar(double percent, long processed, long total)
        {
            int barWidth = 40;
            int filled = (int)(percent / 100 * barWidth);

            Console.CursorLeft = 0;
            Console.Write("[");
            Console.Write(new string('#', filled));
            Console.Write(new string('-', barWidth - filled));
            Console.Write($"] {percent:F1}%  ({processed:N0} / {total:N0})");
        }
    }
}