using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace Clustering
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(DateTime.Now);
                Clustering(args[0], args[1]);//Command line arguments 
            }
            catch (Exception e)
            {
                Clustering(@"C:\Users\K25120\Desktop\Personal\Spoo\files", @"C:\Users\K25120\Desktop\Personal\Spoo\output");
                Console.WriteLine("Error", e);
            }
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("done...");
            Console.ReadKey();
        }

        private static Dictionary<string, string> _stopWords = new Dictionary<string, string>(); // dictionary to hold stop words
        private static List<Document> documentsInCorpus = new List<Document>();
        private static Cluster[] ClusterStore = new Cluster[504];
        private static double[,] SimilarityMatrix = new double[504, 504];
        public class Cluster : List<Document>
        {
        }
        public class Document //: Dictionary<int, Dictionary<string, int>>
        {
            public int docName;
            public Dictionary<string, double> Tokens;
        }
        private static string _outputPath = string.Empty;
        public static void Clustering(string inputPath, string outputPath)
        {
            _outputPath = outputPath;
            string path = @"C:\Users\K25120\Desktop\Personal\Spoo\StopWords.txt";
            using (var file = new System.IO.StreamReader(path))
            {
                string line = string.Empty;
                while ((line = file.ReadLine()) != null)
                {
                    _stopWords.Add(line.Trim(), line); // load all stopwords into dictionary from text file
                }
            }

            DirectoryInfo directory = new DirectoryInfo(inputPath);
            
            foreach (FileInfo file in directory.GetFiles("*.html"))
            {
                var fileName = file.Name.Replace(".html","").TrimStart('0');
                var tokens = new Dictionary<string, double>(); // local dictionary to process tokens for each document

                var input = File.ReadAllText(file.FullName);// read text from html file
                input = HttpUtility.HtmlDecode(input);// perform html decode to extract any special characters that were encoded
                input = ExtractEmailAddresses(input, tokens); // extract email addresses and then remove from input using regex
                input = ExtractUrls(input, tokens);
                input = RemoveHTMLTags(input);// remove html tags using regex
                input = RemovePunctuations(input); // Remove Punctuations from text using regex
                input = Regex.Replace(input, "([.]{2,})*", "", RegexOptions.IgnoreCase);
                input = input.Replace("\n", " ").Replace("\t", " ").Replace("_", " ").Trim(); // remove newline,tabs,period and underscore

                foreach (var token in input.Split(' '))
                {
                    AddToTokensDict(tokens, token);
                }

                var document = new Document() { docName = int.Parse(fileName), Tokens = tokens }; // document has a name and all the tokens in document with occurence count
                var cluster = new Cluster() { document }; // creating a cluster, initially each document is added to its own cluster
                ClusterStore[int.Parse(fileName)] = cluster; // keep track of cluster's inside a datastructure which i am calling a corpus

                documentsInCorpus.Add(document);// this list data structure will be used to get the corpus centroid vector
            }

            PerformAgglomerativeClustering();
        }

        private static void PerformAgglomerativeClustering()
        {
            //sum
            StringBuilder sb = new StringBuilder();
            StringBuilder sbquestions = new StringBuilder();
            Dictionary<string,double> centroid_i = new Dictionary<string,double>();
            Dictionary<string, double> centroid_j = new Dictionary<string, double>();
            double similar = -10000000000;
            double centroidSimilar = -10000000000;
            double dissimilar = 10000000000;
            string closetext = string.Empty;
            string fartext = string.Empty;
            string centroidText = string.Empty;
            int MergeCluster1 = 0;
            int MergeCluster2 = 0;
            var corpusCentroid = GetCentroidVector(documentsInCorpus); // get the centroid vector for corpus
            int pass = 0;
            do
            {
                pass++;
                MergeCluster1 = 0;
                MergeCluster2 = 0;
                similar = -10000000000;
                for (int i = 1; i < ClusterStore.Count(); i++)
                {
                    if (ClusterStore[i] == null) continue; // this situation can happen when we merged one cluster into another. hence the spot where cluster(which was merged) previously existed is empty;
                    //if (pass != 1)
                    //    Debug.Write("i =>" + i + "; pass = " + pass);
                    centroid_i = GetCentroidVector(ClusterStore[i]); // get centroid vector for cluster
                    
                    if (pass == 1)
                    {
                        var centroidSimtemp = Similarity(centroid_i, corpusCentroid); // calculate similarity metric for clusters
                        if (centroidSimtemp > centroidSimilar)
                        {
                            centroidSimilar = centroidSimtemp;
                            centroidText = i + " is closest to corpus centroid; similarity metric = " + centroidSimilar;
                        }
                    }
                    
                    for (int j = 1; j < ClusterStore.Count(); j++)
                    {
                        if (i == j) continue; // do not compare same clusters
                        if (ClusterStore[j] == null) continue; // this situation can happen when we merged one cluster into another. hence the spot where cluster(which was merged) previously existed is empty;
                        //if (pass != 1)
                        //    Debug.Write("i =>" + i + ",j => " + j + "; pass = " + pass);
                        double temp;
                        if (ClusterStore[j].Count > 1 || pass == 1)
                        {
                            centroid_j = GetCentroidVector(ClusterStore[j]);// get centroid vector for cluster
                            temp = Similarity(centroid_i, centroid_j);// calculate similarity metric for clusters
                            SimilarityMatrix[i, j] = temp;// add result to similarity matrix
                        }
                        else
                        {
                            temp = SimilarityMatrix[i, j];// retireve from similarity matrix if already present
                        }
                        
                        
                        if (temp > similar)
                        {
                            similar = temp;
                            closetext = i + "," + j + " are most similar; similarity metric = " + similar;
                            MergeCluster1 = i;
                            MergeCluster2 = j;
                        }
                        
                        if (pass == 1 && temp < dissimilar)
                        {
                            dissimilar = temp;
                            fartext = i + "," + j + " are most dissimilar; similarity metric = " + dissimilar;
                        }
                    }
                }

                if (MergeCluster1 > 0 && MergeCluster2 > 0)
                {
                    sb.AppendLine("Merging clusters " + PrintableCluster(MergeCluster1) + " and " + PrintableCluster(MergeCluster2) + "; similarity metric = " + similar);
                    foreach(Document document in ClusterStore[MergeCluster2])
                    {
                        ClusterStore[MergeCluster1].Add(document); // merging clusters
                    }
                    ClusterStore[MergeCluster2] = null;
                }

                if (pass == 1)
                {
                    sbquestions.AppendLine(closetext);
                    sbquestions.AppendLine(fartext);
                    sbquestions.AppendLine(centroidText);
                }

                if (similar <= 0.4 || pass == 200) 
                    break;

            } while (true);

            sb.AppendLine("===========================================================Cluster Infor Start=============================================");
            pass = 0;

            for (int i = 1; i < ClusterStore.Count(); i++)
            {
                pass++;
                sb.AppendLine("Cluster " + pass + ": " + PrintableCluster(i));
            }

            WriteToFile(sb, "output.txt");
            WriteToFile(sbquestions, "questions.txt");
            
        }

        private static string PrintableCluster(int i)
        {
            if (ClusterStore[i] == null)
            {
                return "(No documents in this cluster)";
            }
            StringBuilder sb = new StringBuilder("");
            foreach(var doc in ClusterStore[i])
            {
                sb.Append(doc.docName + ",");
            }
            return "(" + sb.ToString().TrimEnd(',') + ")";
        }

        private static Dictionary<string, double> GetCentroidVector(List<Document> documents)
        {
            if (documents.Count == 1) // if cluster has only one document then, the document itself is the centroid
                return documents[0].Tokens;
            var centroidTokens = new Dictionary<string, double>();

            //= vector obtained by averaging the weights of the various terms present in the documents of Cluster 
            foreach(var doc in documents)
            {
                foreach(var token in doc.Tokens)
                {
                    if (centroidTokens.ContainsKey(token.Key))
                    {
                        centroidTokens[token.Key] += centroidTokens[token.Key] + token.Value;
                    }
                    else
                    {
                        centroidTokens.Add(token.Key, token.Value);
                    }
                }
            }

            List<string> keys = new List<string>(centroidTokens.Keys);
            foreach (string key in keys)
            {
                centroidTokens[key] = centroidTokens[key] / documents.Count;
            }

            return centroidTokens;
        }


        private static double Similarity(Dictionary<string, double> d1Tokens, Dictionary<string, double> d2Tokens)
        {
            double innerProduct = 0;
            double d1Length = Math.Sqrt(d1Tokens.Sum(t => t.Value * t.Value)); // normalization of vector
            double d2Length = Math.Sqrt(d2Tokens.Sum(t => t.Value * t.Value));// normalization of vector
            foreach(var kvp in d1Tokens)
            {
                if (d2Tokens.ContainsKey(kvp.Key))
                {
                    innerProduct += d2Tokens[kvp.Key] * d1Tokens[kvp.Key]; // dot product of vectors
                }
            }
            return Math.Round(innerProduct / (d1Length * d2Length), 10); // d1.d2/||d1||||d2||
        }

        private static void WriteToFile(StringBuilder sb, string fileName)
        {
            using (var fs = new FileStream(_outputPath, FileMode.Create))
            {
                using (var outputFile = new StreamWriter(fs))
                {
                    outputFile.Write(sb.ToString());
                }
            }
        }


        private static string RemoveHTMLTags(string input)//To Remove HTML tags
        {
            string htmlTags = @"<[a-zA-Z0-9]*\b[^>]*>|</[a-zA-Z0-9]*\b[^>]*>";
            return Regex.Replace(input, htmlTags, " ", RegexOptions.IgnoreCase);
        }
        private static string RemovePunctuations(string input)//Remove Punctuations
        {
            string puntuations = @"[^\w\s.]";
            return Regex.Replace(input, puntuations, " ", RegexOptions.IgnoreCase);
        }

        private static string ExtractUrls(string input, Dictionary<string, double> tokens)//Handle URL's
        {
            string urls = @"http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
            var urlRegex = new Regex(urls, RegexOptions.IgnoreCase);
            MatchCollection urlMatches = urlRegex.Matches(input);

            foreach (Match match in urlMatches)
            {
                AddToTokensDict(tokens, match.Value);
            }
            return Regex.Replace(input, urls, " ", RegexOptions.IgnoreCase);
        }

        private static string ExtractEmailAddresses(string input, Dictionary<string, double> tokens)//Handle Email Addresses
        {
            string emailAddresses = @"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*";
            var emailRegex = new Regex(emailAddresses, RegexOptions.IgnoreCase);
            MatchCollection emailMatches = emailRegex.Matches(input);

            foreach (Match match in emailMatches)
            {
                AddToTokensDict(tokens, match.Value);
            }
            return Regex.Replace(input, emailAddresses, " ", RegexOptions.IgnoreCase);
        }

        private static void AddToTokensDict(Dictionary<string, double> tokens, string token)//Adds tokens to Dictionary
        {
            string tempToken = token.Trim().TrimStart('.').TrimEnd('.').ToLower();

            if (tempToken.Length > 1 && !_stopWords.ContainsKey(tempToken))
            {
                if (!string.IsNullOrEmpty(tempToken))
                {
                    if (tokens.ContainsKey(tempToken))
                        tokens[tempToken] = ++tokens[tempToken];
                    else
                        tokens.Add(tempToken, 1);
                }
            }
        }


    }

  
    
}
