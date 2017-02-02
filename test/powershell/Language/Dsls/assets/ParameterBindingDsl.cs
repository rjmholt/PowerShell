using System.Management.Automation.Language;
using System.Collections.Generic;

[Keyword()]
public class ParameterBindingKeyword : Keyword
{
    public ParameterBindingKeyword()
    {
        RuntimeCall = kwAst => {
            StaticBindingResult bindingResult = ParameterResolutionHelper(kwAst);
            var boundParams = new List<KeyValuePair<string, ParameterBindingResult>>();
            foreach (KeyValuePair<string, ParameterBindingResult> boundParam in bindingResult.BoundParameters)
            {
                boundParams.Add(boundParam);
            }
            return boundParams;
        };
    }
}