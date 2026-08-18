using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Resona.Models;

namespace Resona.Services;

public static class AIService
{
    public class AICleanupResult
    {
        [JsonPropertyName("old")]
        public string Old { get; set; } = "";
        [JsonPropertyName("new")]
        public string New { get; set; } = "";
    }

    public class AIPlaylistResult
    {
        [JsonPropertyName("playlist_name")]
        public string PlaylistName { get; set; } = "";
        
        [JsonPropertyName("track_ids")]
        public List<string> TrackIds { get; set; } = new();
    }

    public static (string Instructions, string JsonData) GenerateCleanupPromptParts(List<string> items, string type)
    {
        if (items == null || items.Count == 0) return ("", "");
        string itemsJson = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        
        string rules = "";
        if (type == "Albums")
        {
            rules = Resona.Models.Strings.Current.CS_AIPromptCleanup_Albums;
        }
        else if (type == "Artistes")
        {
            rules = Resona.Models.Strings.Current.CS_AIPromptCleanup_Artists;
        }
        else if (type == "Genres")
        {
            rules = Resona.Models.Strings.Current.CS_AIPromptCleanup_Genres;
        }

        string instructions = $"Tu es un expert musical. Je vais te donner une liste de noms bruts de la catégorie '{type}'. Ton but est de nettoyer, corriger et regrouper ces noms.\n\n" +
               $"Règles:\n{rules}" +
               "- Retire les parenthèses inutiles.\n\n" +
               "Tu dois OBLIGATOIREMENT renvoyer un tableau JSON contenant des objets avec la propriété 'old' (le nom brut exact) et 'new' (le nom corrigé).\n" +
               "Tu DOIS retourner TOUS les éléments de la liste, sans exception. Si un élément est un single ou ne doit plus faire partie d'un album, met sa propriété 'new' à une chaîne vide \"\". S'il ne nécessite aucune modification, remets le même nom dans 'new'. Format JSON brut uniquement. AUCUN TEXTE AVANT OU APRES LE JSON. ECHAPPE CORRECTEMENT LES GUILLEMETS DANS LES NOMS.";

        string jsonData = "Voici la liste brute :\n" + itemsJson;
        
        return (instructions, jsonData);
    }

	public static Dictionary<string, string> ParseCleanupResponse(string aiResponse)
    {
        var mapping = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(aiResponse)) return mapping;

        try
        {
            int startIndex = aiResponse.IndexOf('[');
            int endIndex = aiResponse.LastIndexOf(']');
            if (startIndex >= 0 && endIndex > startIndex)
            {
                aiResponse = aiResponse.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            
            var results = JsonSerializer.Deserialize<List<AICleanupResult>>(aiResponse, options);
            if (results != null)
            {
                foreach (var res in results)
                {
                    string oldVal = res.Old?.Trim() ?? "";
                    string newVal = res.New?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(oldVal) && oldVal != newVal)
                    {
                        mapping[oldVal] = newVal;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback: robust Regex parsing to handle invalid JSON (like unescaped quotes)
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    aiResponse, 
                    @"\""old\""\s*:\s*\""(.*?)\""\s*,\s*\""new\""\s*:\s*\""(.*?)\""(?=\s*\}|\s*,)", 
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                    
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        string oldVal = match.Groups[1].Value.Trim();
                        string newVal = match.Groups[2].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(oldVal) && oldVal != newVal)
                        {
                            mapping[oldVal] = newVal;
                        }
                    }
                }
            }
            catch { }
            
            if (mapping.Count > 0) return mapping;
            
            throw new Exception("Erreur lors de la lecture du JSON. L'IA a fourni un JSON invalide (souvent des guillemets dans les noms sans les échapper). Détail : " + ex.Message);
        }
        return mapping;
    }

    public static (string Instructions, string JsonData) GeneratePlaylistPromptParts(string userPrompt, List<Track> library)
    {
        if (library == null || library.Count == 0) return ("", "");

        var simplifiedLibrary = library.Select(t => new { id = t.Id, title = t.Title, artist = t.Artist, genre = t.Genre }).ToList();
        string libraryJson = JsonSerializer.Serialize(simplifiedLibrary, new JsonSerializerOptions { WriteIndented = false });

        string instructions = string.Format(Resona.Models.Strings.Current.CS_AIPromptDJ, userPrompt);
               
        string jsonData = "Bibliothèque :\n" + libraryJson;

        return (instructions, jsonData);
    }

    public static AIPlaylistResult? ParsePlaylistResponse(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse)) return null;

        try
        {
            // Remove markdown formatting if present
            string jsonText = aiResponse.Trim();
            if (jsonText.StartsWith("```json")) jsonText = jsonText.Substring(7);
            else if (jsonText.StartsWith("```")) jsonText = jsonText.Substring(3);
            if (jsonText.EndsWith("```")) jsonText = jsonText.Substring(0, jsonText.Length - 3);
            jsonText = jsonText.Trim();
            
            // Fallback for cases where there is extra text
            int startObj = jsonText.IndexOf('{');
            int startArr = jsonText.IndexOf('[');
            int startIndex = -1;
            
            if (startObj >= 0 && startArr >= 0) startIndex = Math.Min(startObj, startArr);
            else if (startObj >= 0) startIndex = startObj;
            else if (startArr >= 0) startIndex = startArr;

            if (startIndex >= 0)
            {
                int endObj = jsonText.LastIndexOf('}');
                int endArr = jsonText.LastIndexOf(']');
                int endIndex = Math.Max(endObj, endArr);
                if (endIndex > startIndex)
                {
                    jsonText = jsonText.Substring(startIndex, endIndex - startIndex + 1);
                }
            }
            
            var node = System.Text.Json.Nodes.JsonNode.Parse(jsonText);
            if (node == null) return null;

            if (node is System.Text.Json.Nodes.JsonArray arrayNode && arrayNode.Count > 0)
            {
                node = arrayNode[0]; // If it's an array, take the first playlist object
            }

            var result = new AIPlaylistResult();
            
            var nameNode = node["playlist_name"] ?? node["name"] ?? node["PlaylistName"] ?? node["Name"];
            if (nameNode != null) result.PlaylistName = nameNode.ToString();

            var tracksNode = node["track_ids"] ?? node["tracks"] ?? node["TrackIds"] ?? node["Tracks"];
            if (tracksNode is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item != null) result.TrackIds.Add(item.ToString());
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText("ai_error_log.txt", $"\nErreur JSON :\n{ex.Message}\nTexte brut:\n{aiResponse}\n");
            return null;
        }
    }
}
