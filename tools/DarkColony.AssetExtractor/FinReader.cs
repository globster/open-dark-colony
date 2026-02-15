using System.Text;

namespace DarkColony.AssetExtractor;

public class FinAnimation
{
    public string Name { get; set; } = string.Empty;
    public int StartFrame { get; set; }
    public int EndFrame { get; set; }
    public int FrameCount => EndFrame - StartFrame + 1;
    public List<FinFrameDetail> FrameDetails { get; set; } = new();
}

public class FinFrameDetail
{
    public string SprFilename { get; set; } = string.Empty;
    public int FrameNumber { get; set; }
    public bool IsMainLayer { get; set; }
    public bool IsFlipped { get; set; }
}

public class FinFile
{
    public List<FinAnimation> Animations { get; set; } = new();
}

public static class FinReader
{
    public static FinFile Read(string filePath)
    {
        return Read(File.ReadAllBytes(filePath));
    }

    public static FinFile Read(byte[] data)
    {
        var fin = new FinFile();

        if (data.Length < 4)
            return fin;

        int offset = 0;

        // Read number of animations (first 4 bytes, uint32 LE)
        int animCount = BitConverter.ToInt32(data, offset);
        offset += 4;

        // Read animation headers (20 bytes each)
        var animHeaders = new List<(string name, int startFrame, int endFrame)>();
        for (int i = 0; i < animCount && offset + 20 <= data.Length; i++)
        {
            // 16-byte null-terminated name
            string name = ReadNullTerminatedString(data, offset, 16);
            offset += 16;

            // 2-byte start frame, 2-byte end frame
            int startFrame = BitConverter.ToUInt16(data, offset);
            offset += 2;
            int endFrame = BitConverter.ToUInt16(data, offset);
            offset += 2;

            animHeaders.Add((name, startFrame, endFrame));
        }

        // Read total frame count (4 bytes)
        int totalFrames = 0;
        if (offset + 4 <= data.Length)
        {
            totalFrames = BitConverter.ToInt32(data, offset);
            offset += 4;
        }

        // Read frame detail blocks (22 bytes each)
        var allFrameDetails = new List<FinFrameDetail>();
        for (int i = 0; i < totalFrames && offset + 22 <= data.Length; i++)
        {
            // 8-byte SPR filename
            string sprFile = ReadNullTerminatedString(data, offset, 8);
            offset += 8;

            // Frame number (2 bytes)
            int frameNum = BitConverter.ToUInt16(data, offset);
            offset += 2;

            // Various flags and metadata (remaining 12 bytes)
            bool isMainLayer = data[offset] != 0;
            offset += 1;
            bool isFlipped = data[offset] != 0;
            offset += 1;

            // Skip remaining 8 bytes of metadata we don't fully understand yet
            offset += 8;

            allFrameDetails.Add(new FinFrameDetail
            {
                SprFilename = sprFile,
                FrameNumber = frameNum,
                IsMainLayer = isMainLayer,
                IsFlipped = isFlipped
            });
        }

        // Map frame details to animations
        foreach (var (name, startFrame, endFrame) in animHeaders)
        {
            var anim = new FinAnimation
            {
                Name = name,
                StartFrame = startFrame,
                EndFrame = endFrame
            };

            for (int f = startFrame; f <= endFrame && f < allFrameDetails.Count; f++)
            {
                anim.FrameDetails.Add(allFrameDetails[f]);
            }

            fin.Animations.Add(anim);
        }

        return fin;
    }

    private static string ReadNullTerminatedString(byte[] data, int offset, int maxLength)
    {
        int end = offset;
        int limit = Math.Min(offset + maxLength, data.Length);
        while (end < limit && data[end] != 0)
            end++;

        return Encoding.ASCII.GetString(data, offset, end - offset).Trim();
    }
}
