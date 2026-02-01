using HidSharp;
using System.Drawing.Imaging;
using System.Runtime.InteropServices; // Для DPI Fix

class Program
{
    // === ТВОИ НАСТРОЙКИ (НЕ МЕНЯЛ) ===
    static int vendorId = 0x0B05;
    static int productId = 0x1AC8;
    static byte MaxBrightness = 200;
    static double SmoothFactor = 0.2;

    // Хардкод 4K, как ты просил
    static int screenW = 3840; 
    static int screenH = 2160; 
    
    // Область захвата из твоего кода
    static int captureW = 200;
    static int captureH = 150;

    static Mutex? singleInstanceMutex;

    // Магия, чтобы координаты 3840x2160 попадали точно в пиксели
    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int processDpiAwareness);

    static void Main()
    {
        // 1. Отключаем масштабирование Windows (чтобы захват был четким)
        try { SetProcessDpiAwareness(2); } catch { }

        // 2. Защита: запускаем только одну копию
        const string appName = "Global\\ASUS_Aura_Ambilight_Release";
        bool createdNew;
        singleInstanceMutex = new Mutex(true, appName, out createdNew);

        // Если уже запущено — тихо выходим
        if (!createdNew) return;

        // 3. Вечный цикл службы (чтобы работало всегда)
        while (true)
        {
            try
            {
                RunServiceLoop();
            }
            catch
            {
                // Если произошла критическая ошибка — ждем и пробуем снова
                Thread.Sleep(5000);
            }
        }
    }

    static void RunServiceLoop()
    {
        var device = DeviceList.Local.GetHidDevices(vendorId, productId).FirstOrDefault();

        // Если лампы нет — спим 2 секунды и ищем снова
        if (device == null || !device.TryOpen(out var stream))
        {
            Thread.Sleep(2000);
            return;
        }

        // === ТВОЙ РАБОЧИЙ КОД ===
        byte[] p35 = new byte[65];
        p35[0]=0xEC; p35[1]=0x35; p35[5]=0x01; p35[8]=0x01;

        byte[] p40 = new byte[65];
        p40[0]=0xEC; p40[1]=0x40; p40[2]=0x84; p40[4]=0x04;

        float curR = 0, curG = 0, curB = 0;
        
        // Координаты центра (считаем один раз)
        int startX = (screenW - captureW) / 2;
        int startY = (screenH - captureH) / 2;

        Bitmap bmp = new Bitmap(captureW, captureH);
        Graphics g = Graphics.FromImage(bmp);

        try
        {
            // Инициализация
            stream.Write(p35); 

            while (true)
            {
                // 1. Скриншот (по твоим координатам)
                g.CopyFromScreen(startX, startY, 0, 0, new Size(captureW, captureH));

                // 2. Анализ
                long r = 0, gr = 0, b = 0;
                int count = 0;

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, captureW, captureH), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                
                unsafe 
                {
                    byte* ptr = (byte*)data.Scan0;
                    int stride = data.Stride;

                    for (int y = 0; y < captureH; y += 10)
                    {
                        for (int x = 0; x < captureW; x += 10)
                        {
                            b += ptr[y * stride + x * 3];
                            gr += ptr[y * stride + x * 3 + 1];
                            r += ptr[y * stride + x * 3 + 2];
                            count++;
                        }
                    }
                }
                bmp.UnlockBits(data);

                if (count > 0) { r /= count; gr /= count; b /= count; }

                // 3. Сглаживание (твои настройки)
                curR = Lerp(curR, (float)r, SmoothFactor);
                curG = Lerp(curG, (float)gr, SmoothFactor);
                curB = Lerp(curB, (float)b, SmoothFactor);

                byte finalR = (byte)Math.Min(curR, MaxBrightness);
                byte finalG = (byte)Math.Min(curG, MaxBrightness);
                byte finalB = (byte)Math.Min(curB, MaxBrightness);

                // 4. Заполнение
                for (int i = 5; i < 62; i += 3)
                {
                    p40[i]     = finalR;
                    p40[i + 1] = finalG;
                    p40[i + 2] = finalB;
                }
                
                p35[9] = finalR; p35[10] = finalG; p35[11] = finalB;

                // 5. Отправка
                stream.Write(p35);
                stream.Write(p40);

                // Твоя задержка (плавность)
                Thread.Sleep(10);
            }
        }
        catch (Exception)
        {
            // Если лампа отключилась или ошибка USB — выходим и ищем заново
            stream.Close();
            throw;
        }
        finally
        {
            g.Dispose();
            bmp.Dispose();
        }
    }

    static float Lerp(float a, float b, double t)
    {
        return a + (float)((b - a) * t);
    }
}