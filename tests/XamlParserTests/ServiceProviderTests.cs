using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace XamlParserTests
{

    public class CallbackExtension
    {
        public object ProvideValue(IServiceProvider provider)
        {
            return provider.GetService<CallbackExtensionCallback>()(provider);
        }
    }

    public delegate object CallbackExtensionCallback(IServiceProvider provider);


    public class ServiceProviderTestsClass
    {
        public string Id { get; set; }
        public object Property { get; set; }
        public ServiceProviderTestsClass Child { get; set; }
        [Content]
        public List<ServiceProviderTestsClass> Children { get; } = new List<ServiceProviderTestsClass>(); 
    }
    
    public class ServiceProviderTests : CompilerTestBase
    {
        void CompileAndRun(string xaml, CallbackExtensionCallback cb, IXamlParentStack parentStack)
            => Compile(xaml).create(new DictionaryServiceProvider
            {
                [typeof(CallbackExtensionCallback)] = cb,
                [typeof(IXamlParentStack)] = parentStack
            });

        class ListParentsProvider : List<object>, IXamlParentStack
        {
            public IEnumerable<object> Parents => this;
        }
        
        [Fact]
        public void Parent_Stack_Should_Provide_Info_About_Parents()
        {
            var importedParents = new ListParentsProvider
            {
                "Parent1",
                "Parent2"
            };
            int num = 0;
            CompileAndRun(@"
<ServiceProviderTestsClass xmlns='test' Id='root' Property='{Callback}'>
    <ServiceProviderTestsClass.Child>
        <ServiceProviderTestsClass Id='direct' Property='{Callback}'/>
    </ServiceProviderTestsClass.Child>
    <ServiceProviderTestsClass Id='content' Property='{Callback}'/> 
</ServiceProviderTestsClass>", sp =>
            {
                //Manual unrolling of enumerable, useful for state tracking
                var stack = new List<object>();
                var parentsEnumerable = sp.GetService<IXamlParentStack>().Parents;
                using (var parentsEnumerator = parentsEnumerable.GetEnumerator())
                {
                    while (parentsEnumerator.MoveNext())
                        stack.Add(parentsEnumerator.Current);
                }
                
                        
                Assert.Equal("Parent1", stack[stack.Count - 2]);
                Assert.Equal("Parent2", stack.Last());
                if (num == 0)
                {
                    Assert.Equal(3, stack.Count);
                    Assert.Equal("root", ((ServiceProviderTestsClass) stack[0]).Id);
                }
                else if (num == 1)
                {
                    Assert.Equal(4, stack.Count);
                    Assert.Equal("direct", ((ServiceProviderTestsClass) stack[0]).Id);
                    Assert.Equal("root", ((ServiceProviderTestsClass) stack[1]).Id);
                }
                else if (num == 2)
                {
                    Assert.Equal(4, stack.Count);
                    Assert.Equal("content", ((ServiceProviderTestsClass) stack[0]).Id);
                    Assert.Equal("root", ((ServiceProviderTestsClass) stack[1]).Id);
                }
                else
                {
                    throw new InvalidOperationException();
                }

                num++;
                
                return "Value";
            }, importedParents);
        }
        
        [Fact]
        public void TypeDescriptor_Stubs_Are_Somewhat_Usable()
        {
            CompileAndRun(@"<ServiceProviderTestsClass xmlns='test' Property='{Callback}'/>", sp =>
            {
                var tdc = (ITypeDescriptorContext) sp;
                Assert.Equal(tdc, sp.GetService<ITypeDescriptorContext>());
                Assert.Null(tdc.Instance);
                Assert.Null(tdc.Container);
                Assert.Null(tdc.PropertyDescriptor);
                Assert.Throws<NotSupportedException>(() => tdc.OnComponentChanging());
                Assert.Throws<NotSupportedException>(() => tdc.OnComponentChanged());
                
                return "Value";
            }, null);
        }
        
    }
}