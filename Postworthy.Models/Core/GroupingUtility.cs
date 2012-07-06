using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections;

namespace Postworthy.Models.Core
{
    public static class GroupingUtility
    {
        private const int MIN_LENGTH = 3;
        private const decimal MINIMUM = .40M;
        private const decimal GOOD = .46M;
        private const decimal STRONG = .75M;

        private class SimilarObject<T> where T : ISimilarText
        {
            public T Object { get; set; }
            public T ParentObject { get; set; }
            public decimal SimilarityIndex { get; set; }
        }

        public class SimilarObjects<T> : List<T>, IGrouping<T, T> where T : ISimilarText
        {
            public T Key { get; set; }
        }


        public static IEnumerable<IGrouping<T, T>> GroupSimilar<T>(this IEnumerable<T> t) where T : ISimilarText
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

                #region Initialize SimilarityObjects
                so = new SimilarObject<T>[soLength];

                for (int i = 0; i < soLength; i++)
                {
                    so[i] = new SimilarObject<T>();
                    so[i].Object = input[i];
                    so[i].SimilarityIndex = 0;
                }
                #endregion

                #region Pass 1
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
                                p1c = so[i].Object.WordLetterPairHash.Count;
                                p2c = so[j].Object.WordLetterPairHash.Count;
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
                                    if (si >= GOOD)
                                    {
                                        so[j].ParentObject = so[i].Object;
                                        so[j].SimilarityIndex = si;
                                    }
                                }
                                #endregion
                            }
                        }
                    }
                }
                #endregion
            }
            #endregion

            #region Create Groups
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

            return groups;
        }
    }
}