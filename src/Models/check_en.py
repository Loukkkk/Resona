import re

with open(r'C:\Users\louka\LOUKA\Resona\src\Models\Strings.cs', 'r', encoding='utf-8') as f:
    content = f.read()

pattern = re.compile(r'=> IsFr \? "(.*?)" : "(.*?)";')
for match in pattern.finditer(content):
    fr = match.group(1)
    en = match.group(2)
    # Check if they are exactly identical but shouldn't be
    if fr == en and fr not in ["Albums", "Autotag", "ffmpeg", "yt-dlp", "Resona", "URL", "100 morceaux", "300 morceaux", "500 morceaux", "1000 morceaux", "2000 morceaux", "128 kbps", "192 kbps", "256 kbps", "320 kbps", "Album"]:
        print(f"Identical FR/EN: {en}")

EOF
