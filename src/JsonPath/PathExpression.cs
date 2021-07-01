﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
        
namespace JsonCons.JsonPathLib
{
    class StaticResources : IDisposable
    {
        private bool _disposed = false;
        IList<IDisposable> _disposables = new List<IDisposable>();

        internal JsonElement CreateJsonElement(string json)
        {
            var doc = JsonDocument.Parse(json); 
            _disposables.Add(doc);
            return doc.RootElement;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    foreach (var item in _disposables)
                    {
                        item.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        ~StaticResources()
        {
            Dispose(false);
        }
    };

    interface IPathExpression : IDisposable
    {
        IReadOnlyList<JsonElement> Select(JsonElement root, ResultOptions options);
    };

    public class PathExpression : IPathExpression
    {
        private bool _disposed = false;
        ISelector _selector;

        internal PathExpression(ISelector selector)
        {
            _selector = selector;
        }

        public IReadOnlyList<JsonElement> Select(JsonElement root, ResultOptions options)
        {
            PathNode pathTail = new PathNode("$");
            var values = new List<JsonElement>();

            if ((options & ResultOptions.Sort | options & ResultOptions.NoDups) != 0)
            {
                var nodes = new List<JsonPathNode>();
                INodeAccumulator accumulator = new NodeAccumulator(nodes);
                _selector.Select(root, pathTail, root, accumulator, options);

                if (nodes.Count > 1)
                {
                    if ((options & ResultOptions.Sort) == ResultOptions.Sort)
                    {
                        nodes.Sort();
                    }
                    if ((options & ResultOptions.NoDups) == ResultOptions.NoDups)
                    {
                        var index = new HashSet<JsonPathNode>(nodes);
                        foreach (var node in nodes)
                        {
                            if (index.Contains(node))
                            {
                                values.Add(node.Value);
                                index.Remove(node);
                            }
                        }
                    }
                    else
                    {
                        foreach (var node in nodes)
                        {
                            values.Add(node.Value);
                        }
                    }
                }
                else
                {
                    foreach (var node in nodes)
                    {
                        values.Add(node.Value);
                    }
                }
            }
            else
            {
                INodeAccumulator accumulator = new ValueAccumulator(values);            
                _selector.Select(root, pathTail, root, accumulator, options);
            }

            return values;
        }

        public IReadOnlyList<NormalizedPath> SelectPaths(JsonElement root, ResultOptions options)
        {
            PathNode pathTail = new PathNode("$");
            var paths = new List<NormalizedPath>();
            INodeAccumulator accumulator = new PathAccumulator(paths);
            _selector.Select(root, pathTail, root, accumulator, options | ResultOptions.Path);

            if ((options & ResultOptions.Sort | options & ResultOptions.NoDups) != 0)
            {
                if (paths.Count > 1)
                {
                    if ((options & ResultOptions.Sort) == ResultOptions.Sort)
                    {
                        paths.Sort();
                    }
                    if ((options & ResultOptions.NoDups) == ResultOptions.NoDups)
                    {
                        var temp = new List<NormalizedPath>();
                        var index = new HashSet<NormalizedPath>(paths);
                        foreach (var path in paths)
                        {
                            if (index.Contains(path))
                            {
                                temp.Add(path);
                                index.Remove(path);
                            }
                        }
                        paths = temp;
                    }
                }
            }

            return paths;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    //foreach (var item in _disposables)
                    //{
                    //    item.Dispose();
                    //}
                }
                _disposed = true;
            }
        }

        ~PathExpression()
        {
            Dispose(false);
        }
    }

} // namespace JsonCons.JsonPathLib
