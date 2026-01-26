using System.Diagnostics.CodeAnalysis;

// Test naming conventions use underscores for readability
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method names use underscores for readability", Scope = "namespaceanddescendants", Target = "~N:Neuralium.Tests")]

// Test classes may own disposable resources that are cleaned up differently
[assembly: SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Test classes handle disposal appropriately", Scope = "namespaceanddescendants", Target = "~N:Neuralium.Tests")]
