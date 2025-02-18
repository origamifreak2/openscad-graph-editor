using System.Collections.Generic;
using System.Linq;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Library.External;
using OpenScadGraphEditor.Utils;

namespace OpenScadGraphEditor.Refactorings
{
    public class DeleteExternalReferenceRefactoring : Refactoring
    {
        private readonly ExternalReference _toDelete;

        public DeleteExternalReferenceRefactoring(ExternalReference toDelete) 
        {
            _toDelete = toDelete;
        }

        public override void PerformRefactoring(RefactoringContext context)
        {
            // we make a new dictionary of includes based on the one we have minus the one we want to delete
            var filesToImport = new Dictionary<string, IncludeMode>();
            // we only look at the top level imports, all transitive imports will be just deleted and then
            // re-added should they still exist in the files
            context.Project.ExternalReferences
                .Where(it => it != _toDelete)
                .Where(it => !it.IsTransitive)
                .ForAll(import => filesToImport[import.IncludePath] = import.Mode);
            
            // we have now a new list of files to import, so we can just remove all imports and then add them again
            context.PerformRefactoring(new ReplaceExternalReferencesRefactoring(filesToImport));
        }
    }
}