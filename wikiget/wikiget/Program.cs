using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace wikiget
{
    class Program
    {
        static int Main(string[] args)
        {
            string articlesDir = Path.Combine(AppContext.BaseDirectory, "wikipedia_texts");
            string canonicalFile = Path.Combine(AppContext.BaseDirectory, "canonical_all.txt");
            const int targetLines = 10;
            const int maxFullSize = 1536;
            double relevantProb = 0.7;
            double nonRelevantProb = 0.3;

            if (args.Length == 0)
            {
                Console.WriteLine("Укажите название статьи в качестве аргумента.");
                Console.WriteLine("Пример: wikiget.exe \"Сознание\"");
                return 1;
            }
            string originalQuery = args[0].Trim();
            if (string.IsNullOrEmpty(originalQuery))
            {
                Console.WriteLine("Пустой запрос.");
                return 1;
            }

            // Список запросов для последовательного перебора
            var queriesToTry = new List<string>();

            // 1. Исходный запрос
            queriesToTry.Add(originalQuery);

            // 2. Если исходный запрос состоит из двух слов через пробел, пробуем "Фамилия, Имя"
            var words = originalQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2)
            {
                string inverted = $"{words[1]}, {words[0]}";
                queriesToTry.Add(inverted);
            }

            // 3. Затем пробуем отдельные слова (от длинного к короткому)
            //    (исключая уже использованные варианты)
            var distinctWords = words
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .Distinct()
                .OrderByDescending(w => w.Length)
                .ToList();
            foreach (var word in distinctWords)
            {
                if (!queriesToTry.Contains(word))
                    queriesToTry.Add(word);
            }

            // Теперь перебираем все варианты запросов
            foreach (string query in queriesToTry)
            {
                // Прямой путь для текущего query
                string directPath = Path.Combine(articlesDir, query + ".txt");
                if (File.Exists(directPath))
                {
                    return ProcessArticle(directPath, originalQuery, maxFullSize, targetLines, relevantProb, nonRelevantProb);
                }

                // Сбор кандидатов из canonical_all.txt
                var candidates = new List<(string canonical, char type)>();

                if (File.Exists(canonicalFile))
                {
                    using var sr = new StreamReader(canonicalFile, Encoding.UTF8);
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        int pipeIdx = line.IndexOf('|');
                        if (pipeIdx <= 0) continue;

                        string canonical = line.Substring(0, pipeIdx).Trim();
                        string rest = line.Substring(pipeIdx + 1).TrimStart();
                        if (rest.Length == 0) continue;
                        char type = rest[0];
                        if (type != 'i' && type != 'r') continue;

                        // Проверка канонического имени
                        if (canonical.Equals(query, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add((canonical, type));
                            continue;
                        }

                        // Проверка редиректов (слова после типа)
                        string[] parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (parts[i].Equals(query, StringComparison.OrdinalIgnoreCase))
                            {
                                candidates.Add((canonical, type));
                                break;
                            }
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    var rnd = new Random();
                    // Перемешиваем кандидатов
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        int swapIdx = rnd.Next(i, candidates.Count);
                        var temp = candidates[i];
                        candidates[i] = candidates[swapIdx];
                        candidates[swapIdx] = temp;
                    }

                    // Пробуем по очереди
                    foreach (var (canonical, type) in candidates)
                    {
                        string path = Path.Combine(articlesDir, canonical + ".txt");
                        if (File.Exists(path))
                        {
                            if (type == 'r')
                                return PrintFullFile(path);
                            else
                                return PrintInspect(path, originalQuery, targetLines, relevantProb, nonRelevantProb);
                        }
                    }
                }
            }

            // Если ничего не найдено
            Console.WriteLine($"Статья «{originalQuery}» не найдена.");
            return 1;
        }

        static int ProcessArticle(string path, string keyword, int maxFullSize, int targetLines, double relProb, double nonRelProb)
        {
            var fi = new FileInfo(path);
            if (fi.Length <= maxFullSize)
                return PrintFullFile(path);
            else
                return PrintInspect(path, keyword, targetLines, relProb, nonRelProb);
        }

        static int PrintFullFile(string path)
        {
            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);
                Console.WriteLine(content);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка чтения файла: {ex.Message}");
                return 1;
            }
        }

        static int PrintInspect(string path, string keyword, int targetLines, double relProb, double nonRelProb)
        {
            try
            {
                var selected = new List<string>();
                var random = new Random();

                using var reader = new StreamReader(path, Encoding.UTF8);
                string? line;
                while (selected.Count < targetLines && (line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    bool containsKeyword = trimmed.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    double prob = containsKeyword ? relProb : nonRelProb;

                    if (random.NextDouble() < prob)
                    {
                        selected.Add(trimmed);
                    }
                }

                if (selected.Count == 0)
                    Console.WriteLine("Не найдено строк.");
                else
                    foreach (var s in selected)
                        Console.WriteLine("- " + s);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке статьи: {ex.Message}");
                return 1;
            }
        }
    }
}