using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections;
using AForge.Imaging;
using System.IO;

namespace Postworthy.Models.Core
{
    public static class GroupingUtility
    {
        private const int MIN_LENGTH = 3;
        private const decimal MINIMUM = .40M;
        private const decimal GOOD = .46M;
        private const decimal STRONG = .75M;

        private class SimilarObject<T> where T : ISimilarText, ISimilarImage, ISimilarLinks
        {
            public T Object { get; set; }
            public T ParentObject { get; set; }
            public decimal SimilarityIndex { get; set; }
            public int PassNumber { get; set; }
        }

        public class SimilarObjects<T> : List<T>, IGrouping<T, T> where T : ISimilarText, ISimilarImage, ISimilarLinks
        {
            public T Key { get; set; }
        }

        private class FlaggedObject<T> where T : ISimilarText, ISimilarImage, ISimilarLinks
        {
            public T Object { get; set; }
            public bool Flagged { get; set; }
        }


        public static IEnumerable<IGrouping<T, T>> GroupSimilar<T>(this IEnumerable<T> t, decimal MinSimilarity = GOOD, TextWriter log = null) where T : ISimilarText, ISimilarImage, ISimilarLinks
        {
            #region Variable Definitions
            var input = t.ToList();
            StringBuilder update = new StringBuilder("");
            var groups = new List<SimilarObjects<T>>();
            decimal si;
            int soLength = 0;
            int intersection;
            int p1c;
            int p2c;
            int union;
            SimilarObject<T>[] so = null;
            #endregion

            #region Process Data
            if (input != null && input.Count() > 0)
            {
                soLength = input.Count();

                if (log != null) 
                    log.WriteLine("{0}: [GroupSimilar] Initializing SimilarityObjects", DateTime.Now); 

                #region Initialize SimilarityObjects
                so = new SimilarObject<T>[soLength];

                for (int i = 0; i < soLength; i++)
                {
                    so[i] = new SimilarObject<T>();
                    so[i].Object = input[i];
                    so[i].SimilarityIndex = 0;
                }
                #endregion

                if (log != null) 
                    log.WriteLine("{0}: [GroupSimilar] Comparing {1} items", DateTime.Now, soLength);

                #region Compare Objects
                for (int i = 0; i < soLength; i++)
                {
                    if (so[i].ParentObject == null) // Only Tweets that are not already assigned to a parent should be processed
                    {
                        for (int j = (i + 1); j < soLength; j++)
                        {
                            if (so[j].ParentObject == null) //If it already has a parent then no need to try to find it another parent
                            {
                                #region Compare All Pairs and Assign Similarity (Pass 1)
                                intersection = 0;
                                p1c = so[i].Object.WordLetterPairHash.Length;
                                p2c = so[j].Object.WordLetterPairHash.Length;
                                union = p1c + p2c;

                                if (union != 0)
                                {
                                    for (int k = 0; k < p1c; k++)
                                    {
                                        for (int l = 0; l < p2c; l++)
                                        {
                                            if (so[i].Object.WordLetterPairHash[k] == so[j].Object.WordLetterPairHash[l])
                                            {
                                                intersection++;
                                                break;
                                            }
                                        }
                                    }
                                    si = (2.0M * intersection) / union;
                                    if (si >= MinSimilarity)
                                    {
                                        so[j].ParentObject = so[i].Object;
                                        so[j].SimilarityIndex = si;
                                        so[j].PassNumber = 1;
                                        break;
                                    }
                                }
                                #endregion
                                #region Compare Images and Assign Similarity (Pass 2)
                                if(so[i].Object.Image != null && so[j].Object.Image != null)
                                {
                                    var etm = new ExhaustiveTemplateMatching(0);
                                    TemplateMatch[] matchings = etm.ProcessImage(so[i].Object.Image, so[j].Object.Image);
                                    if (matchings[0].Similarity > 0.90)
                                    {
                                        so[j].ParentObject = so[i].Object;
                                        so[j].SimilarityIndex = Convert.ToDecimal(matchings[0].Similarity);
                                        so[j].PassNumber = 2;
                                        break;
                                    }
                                }
                                #endregion
                                #region Compare Links and Assign Similarity (Pass 3)
                                if (so[i].Object.Links != null && so[j].Object.Links != null)
                                {
                                    var iLinks = so[i].Object.Links.Select(x => x);
                                    var jLinks = so[j].Object.Links.Select(x => x);
                                    var linkCount = iLinks.Count() + jLinks.Count();
                                    if (linkCount > 0)
                                    {
                                        var matchPercentage = (2.0M * iLinks.Count(x => jLinks.Any(y=>y.Equals(x)))) / linkCount;
                                        if (matchPercentage != 0)
                                        {
                                            so[j].ParentObject = so[i].Object;
                                            so[j].SimilarityIndex = matchPercentage;
                                            so[j].PassNumber = 3;
                                            break;
                                        }
                                    }
                                }
                                #endregion
                            }
                        }
                    }
                }
                #endregion

                if (log != null)
                {
                    log.WriteLine("{0}: [GroupSimilar] Compared {1} items and found {2} with similar text, {3} with similar images, and {4} with similar links", 
                        DateTime.Now, 
                        soLength, 
                        so.Count(x=>x.PassNumber == 1),
                        so.Count(x => x.PassNumber == 2),
                        so.Count(x => x.PassNumber == 3));
                }
            }
            #endregion

            #region Create Groups
            
            if (log != null)
                log.WriteLine("{0}: [GroupSimilar] Creating Groups", DateTime.Now, soLength);

            if (so != null && soLength > 0)
            {
                for (int i = 0; i < soLength; i++)
                {
                    if (so[i].ParentObject != null)
                    {
                        if (groups.Where(g => g.Key.Equals(so[i].ParentObject)).Count() == 0)
                            groups.Add(new SimilarObjects<T>() { Key = so[i].ParentObject });

                        groups.FirstOrDefault(g => g.Key.Equals(so[i].ParentObject)).Add(so[i].Object);
                    }
                    else
                    {
                        var similarObjects = new SimilarObjects<T>() { Key = so[i].Object };
                        similarObjects.Add(so[i].Object);
                        groups.Add(similarObjects);
                    }
                }
            }
            #endregion

            if (log != null)
                log.WriteLine("{0}: [GroupSimilar] Returning Groups", DateTime.Now, soLength);

            return groups;
        }

        public static IEnumerable<IGrouping<T, T>> GroupSimilar2<T>(this IEnumerable<T> t, decimal MinSimilarity = GOOD) where T : ISimilarText, ISimilarImage, ISimilarLinks
        {
            var groups = new List<SimilarObjects<T>>();
            var items = t.Select(x => new FlaggedObject<T>() { Object = x }).ToList();

            foreach(var item in items)
            {
                if(!item.Flagged)
                {
                    item.Flagged = true;
                    var matches = items.Where(x => !x.Flagged && x != item && x.Object.IsSimilar(item.Object));
                    if(matches.FirstOrDefault() != null)
                    {
                        var so = new SimilarObjects<T>() { Key = item.Object };
                        foreach(var match in matches)
                        {
                            match.Flagged = true;
                            so.Add(match.Object);
                        }
                        groups.Add(so);
                    }
                    else
                        groups.Add(new SimilarObjects<T>() { Key = item.Object });
                }
            }

            return groups;
        }

        private static bool IsSimilar<T>(this T t1, T t2, decimal MinSimilarity = GOOD) where T : ISimilarText, ISimilarImage, ISimilarLinks
        {
            #region Compare All Pairs and Assign Similarity (Pass 1)
            int intersection = 0;
            int union = t1.WordLetterPairHash.Length + t2.WordLetterPairHash.Length;
            int p1c = 0;
            int p2c = 0;
            int[] wlp1 = null;
            int[] wlp2 = null;
            decimal earlyExitCheckValue = 0;
            //We want the shortest collection to the the outter collection
            if(t1.WordLetterPairHash.Length < t2.WordLetterPairHash.Length)
            {
                p1c = t1.WordLetterPairHash.Length;
                p2c = t2.WordLetterPairHash.Length;
                wlp1 = t1.WordLetterPairHash;
                wlp2 = t2.WordLetterPairHash;
            }
            else
            {
                p1c = t2.WordLetterPairHash.Length;
                p2c = t1.WordLetterPairHash.Length;
                wlp1 = t2.WordLetterPairHash;
                wlp2 = t1.WordLetterPairHash;
            }

            earlyExitCheckValue = (MinSimilarity * union) / 2.0M;

            if (union != 0)
            {
                for (int k = 0; k < p1c; k++)
                {
                    for (int l = 0; l < p2c; l++)
                    {
                        if (wlp1[k] == wlp2[l])
                        {
                            intersection++;
                            break;
                        }
                    }

                    if (intersection >= earlyExitCheckValue)
                        return true;
                    else if (intersection + (p1c-k) < earlyExitCheckValue)
                        break;
                }
                //decimal si = (2.0M * intersection) / union;
                //if (si >= MinSimilarity)
                //    return true;
            }
            #endregion
            #region Compare Links and Assign Similarity (Pass 2)
            if (t1.Links != null && t2.Links != null)
            {
                var iLinks = t1.Links.Select(x => x);
                var jLinks = t2.Links.Select(x => x);
                var linkCount = iLinks.Count() + jLinks.Count();
                if (linkCount > 0)
                {
                    var matchPercentage = (2.0M * iLinks.Count(x => jLinks.Any(y => y.Equals(x)))) / linkCount;
                    if (matchPercentage != 0)
                        return true;
                }
            }
            #endregion
            #region Compare Images and Assign Similarity (Pass 3)
            if (t1.Image != null && t2.Image != null)
            {
                var etm = new ExhaustiveTemplateMatching(0);
                TemplateMatch[] matchings = etm.ProcessImage(t1.Image, t2.Image);
                if (matchings[0].Similarity > 0.90)
                    return true;
            }
            #endregion
            return false;
        }
    }
}