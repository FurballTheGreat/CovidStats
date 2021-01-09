using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.CodeDom.Compiler
{
    public class CompilerErrorCollection : List<CompilerError>
    {
        public bool HasErrors => false;
    }

    public class CompilerError
    {
        public string ErrorText { get; set; }

        public bool IsWarning { get; set; }
    }


}

namespace System.Runtime.Remoting.Messaging
{
    public class CallContext
    {
        private static ThreadLocal<Dictionary<string,object>> _logicalData = new ThreadLocal<Dictionary<string, object>>(()=>new Dictionary<string, object>());
        public static object LogicalGetData(string pName)
        {
            if (_logicalData.Value.ContainsKey(pName))
                return _logicalData.Value[pName];
            throw new InvalidOperationException($"{pName} does not exist in the thread local logical data");
        }

        public static void Add(string pName, object pValue)
        {
            if (_logicalData.Value.ContainsKey(pName))
                _logicalData.Value.Remove(pName);
            _logicalData.Value.Add(pName, pValue);
        }

    }
}
