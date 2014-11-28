using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elmah;
using StackifyLib;
using System.Collections;
using StackifyLib.Utils;

namespace StackifyLib.ELMAH
{
    public class StackifyErrorLog : ErrorLog
    {
        private static readonly ErrorGovernor governor = new ErrorGovernor();

        public StackifyErrorLog(){}
        
        public StackifyErrorLog(IDictionary config) 
        { 
        }

        public override ErrorLogEntry GetError(string id)
        {
            throw new NotImplementedException();
        }

        public override int GetErrors(int pageIndex, int pageSize, System.Collections.IList errorEntryList)
        {
            throw new NotImplementedException();
        }

        public override string Log(Error e)
        {
            try
            {
                StackifyError error = StackifyError.New(e.Exception);

                if (governor.ErrorShouldBeSent(error))
                {
                    error.SendToStackify();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            var newId = Guid.NewGuid();
            return newId.ToString();
        }


    }
}
