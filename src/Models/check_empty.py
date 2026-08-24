import re

with open(r'C:\Users\louka\LOUKA\Resona\src\Models\Strings.cs', 'r', encoding='utf-8') as f:
    content = f.read()

lines = content.split('\n')
for i, line in enumerate(lines):
    if 'IsFr ?' in line:
        parts = line.split(':')
        if len(parts) > 1:
            en_part = parts[-1].strip()
            if en_part == '""' or en_part == '"";':
                print(f"Empty translation on line {i+1}: {line.strip()}")

for i, line in enumerate(lines):
    if 'IsFr ?' in line:
        fr_part = line.split('?')[1].split(':')[0].strip().strip('"')
        en_part = line.split(':')[-1].strip().strip(';').strip().strip('"')
        
        # Look for suspicious French words in English translations
        suspicious_words = ["Rechercher", "Télécharger", "Dossiers", "Bibliothèque", "Paramètres", "Changer", "Choisir", "Piste", "Terme", "Écraser", "Enregistrer"]
        for word in suspicious_words:
            if word in en_part:
                print(f"Suspicious word '{word}' in English translation on line {i+1}: {line.strip()}")

