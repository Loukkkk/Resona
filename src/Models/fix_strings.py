import re

with open(r'C:\Users\louka\LOUKA\Resona\src\Models\Strings.cs', 'r', encoding='utf-8') as f:
    content = f.read()

def fix_text(text):
    text = text.replace('ÃƒÂ©', 'é')
    text = text.replace('ÃƒÂ¨', 'è')
    text = text.replace('ÃƒÂª', 'ê')
    text = text.replace('ÃƒÂ ', 'à')
    text = text.replace('ÃƒÂ®', 'î')
    text = text.replace('ÃƒÂ§', 'ç')
    text = text.replace('Ãƒâ€°', 'É')
    text = text.replace('Ã¢â‚¬Â¢', '•')
    text = text.replace(r'\u00E0', 'à')
    text = text.replace(r'\u00C9', 'É')
    text = text.replace('Mise  jour', 'Mise à jour')
    text = text.replace('Ajouter  une', 'Ajouter à une')
    text = text.replace('Ajouter  la', 'Ajouter à la')
    text = text.replace('Bienvenue dansà Resona', 'Bienvenue dans Resona')
    text = text.replace('Welcome toà Resona', 'Welcome to Resona')
    text = text.replace('CrÃ©er', 'Créer')
    text = text.replace('BibliothÃ¨que', 'Bibliothèque')
    
    return text

fixed_content = fix_text(content)

if "Album inconnu" not in fixed_content:
    fixed_content = fixed_content.replace('public string CS_Album =>', 'public string CS_AlbumInconnu => IsFr ? "Album inconnu" : "Unknown album";\n    public string CS_ArtisteInconnu => IsFr ? "Artiste inconnu" : "Unknown artist";\n    public string CS_Album =>')

with open(r'C:\Users\louka\LOUKA\Resona\src\Models\Strings.cs', 'w', encoding='utf-8') as f:
    f.write(fixed_content)

print("Done")
