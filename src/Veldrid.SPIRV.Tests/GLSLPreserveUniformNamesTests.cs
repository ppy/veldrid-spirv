// Tests for GLSL uniform name preservation
//
// When compiling SPIRV to GLSL for OpenGL/GLES targets, uniform block names must be preserved
// to allow runtime shader loading without requiring the SPIRV compiler at runtime.
//
// Applications that pre-compile shaders to GLSL at build time need to query uniform blocks
// by name using glGetUniformBlockIndex(program, "BlockName"). If the block names are not
// preserved during SPIRV->GLSL compilation, the runtime won't be able to bind uniforms correctly.

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Veldrid.SPIRV.Tests
{
    public class GLSLPreserveUniformNamesTests
    {
        private readonly ITestOutputHelper _output;

        public GLSLPreserveUniformNamesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void UniformNames_PreservedInGLSL_WithoutNormalization()
        {
            byte[] vsBytes = TestUtil.LoadBytes("planet.vert");
            byte[] fsBytes = TestUtil.LoadBytes("planet.frag");

            VertexFragmentCompilationResult result = SpirvCompilation.CompileVertexFragment(
                vsBytes,
                fsBytes,
                CrossCompileTarget.GLSL,
                new CrossCompileOptions(false, false, normalizeResourceNames: false));

            _output.WriteLine("=== VERTEX SHADER OUTPUT ===");
            _output.WriteLine(result.VertexShader);
            _output.WriteLine("");
            _output.WriteLine("=== FRAGMENT SHADER OUTPUT ===");
            _output.WriteLine(result.FragmentShader);
            _output.WriteLine("");

            // Check that original uniform BLOCK names are preserved (for glGetUniformBlockIndex)
            Assert.Contains("uniform ProjView", result.VertexShader);
            Assert.Contains("uniform ProjView", result.FragmentShader);
            Assert.Contains("uniform LightInfo", result.FragmentShader);

            // Check that they're NOT normalized to vdspv_ names
            Assert.DoesNotContain("vdspv_0_0", result.VertexShader);
            Assert.DoesNotContain("vdspv_0_0", result.FragmentShader);
            Assert.DoesNotContain("vdspv_0_2", result.FragmentShader);

            // Verify that uniform members are accessible (instance name may have suffix, that's OK)
            // The important thing is the block type name is preserved
            Assert.Contains(".View", result.VertexShader);
            Assert.Contains(".Proj", result.VertexShader);
            Assert.Contains(".LightDirection", result.FragmentShader);
            Assert.Contains(".CameraPosition", result.FragmentShader);
        }

        [Fact]
        public void UniformNames_PreservedWithDebugInfo()
        {
            // Test that names are preserved when compiling GLSL→SPIRV with debug info (debug: true)
            string vertexGlsl = TestUtil.LoadShaderText("planet.vert");
            string fragmentGlsl = TestUtil.LoadShaderText("planet.frag");

            // Step 1: Compile GLSL → SPIRV with debug info (debug: true)
            SpirvCompilationResult vertexSpirvResult = SpirvCompilation.CompileGlslToSpirv(
                vertexGlsl, "planet.vert", ShaderStages.Vertex, new GlslCompileOptions(debug: true));
            SpirvCompilationResult fragmentSpirvResult = SpirvCompilation.CompileGlslToSpirv(
                fragmentGlsl, "planet.frag", ShaderStages.Fragment, new GlslCompileOptions(debug: true));

            // Step 2: Cross-compile SPIRV → GLSL
            VertexFragmentCompilationResult result = SpirvCompilation.CompileVertexFragment(
                vertexSpirvResult.SpirvBytes,
                fragmentSpirvResult.SpirvBytes,
                CrossCompileTarget.GLSL,
                new CrossCompileOptions(false, false, normalizeResourceNames: false));

            _output.WriteLine("=== VERTEX (GLSL→SPIRV[debug:true]→GLSL) ===");
            _output.WriteLine(result.VertexShader);
            _output.WriteLine("");
            _output.WriteLine("=== FRAGMENT (GLSL→SPIRV[debug:true]→GLSL) ===");
            _output.WriteLine(result.FragmentShader);
            _output.WriteLine("");

            // Check that uniform block names are preserved with debug info
            Assert.Contains("uniform ProjView", result.VertexShader);
            Assert.Contains("uniform LightInfo", result.FragmentShader);
            Assert.DoesNotContain("_RESERVED_IDENTIFIER_FIXUP_", result.VertexShader);
            Assert.DoesNotContain("_RESERVED_IDENTIFIER_FIXUP_", result.FragmentShader);
            Assert.DoesNotContain("vdspv_0_0", result.VertexShader);
        }

        [Fact]
        public void UniformNames_PreservedWithOptimization()
        {
            // Test that names are STILL preserved even when compiling GLSL→SPIRV with optimization (debug: false)
            // This works because we always call SetGenerateDebugInfo() to preserve OpName instructions,
            // even when optimization is enabled.
            string vertexGlsl = TestUtil.LoadShaderText("planet.vert");
            string fragmentGlsl = TestUtil.LoadShaderText("planet.frag");

            // Step 1: Compile GLSL → SPIRV with optimization (debug: false)
            SpirvCompilationResult vertexSpirvResult = SpirvCompilation.CompileGlslToSpirv(
                vertexGlsl, "planet.vert", ShaderStages.Vertex, new GlslCompileOptions(debug: false));
            SpirvCompilationResult fragmentSpirvResult = SpirvCompilation.CompileGlslToSpirv(
                fragmentGlsl, "planet.frag", ShaderStages.Fragment, new GlslCompileOptions(debug: false));

            // Step 2: Cross-compile SPIRV → GLSL
            VertexFragmentCompilationResult result = SpirvCompilation.CompileVertexFragment(
                vertexSpirvResult.SpirvBytes,
                fragmentSpirvResult.SpirvBytes,
                CrossCompileTarget.GLSL,
                new CrossCompileOptions(false, false, normalizeResourceNames: false));

            _output.WriteLine("=== VERTEX (GLSL→SPIRV[debug:false+optimized]→GLSL) ===");
            _output.WriteLine(result.VertexShader);
            _output.WriteLine("");
            _output.WriteLine("=== FRAGMENT (GLSL→SPIRV[debug:false+optimized]→GLSL) ===");
            _output.WriteLine(result.FragmentShader);
            _output.WriteLine("");

            // Check that uniform block names are STILL preserved even with optimization
            Assert.Contains("uniform ProjView", result.VertexShader);
            Assert.Contains("uniform LightInfo", result.FragmentShader);
            Assert.DoesNotContain("_RESERVED_IDENTIFIER_FIXUP_", result.VertexShader);
            Assert.DoesNotContain("_RESERVED_IDENTIFIER_FIXUP_", result.FragmentShader);
            Assert.DoesNotContain("vdspv_0_0", result.VertexShader);
        }

        [Fact]
        public void UniformNames_PreservedInESSL_WithOptimization()
        {
            // Test that names are preserved for OpenGL ES (ESSL) targets too
            // ESSL is used on mobile/embedded devices and has the same requirement
            string vertexGlsl = TestUtil.LoadShaderText("planet.vert");
            string fragmentGlsl = TestUtil.LoadShaderText("planet.frag");

            // Step 1: Compile GLSL → SPIRV with optimization (debug: false)
            SpirvCompilationResult vertexSpirvResult = SpirvCompilation.CompileGlslToSpirv(
                vertexGlsl, "planet.vert", ShaderStages.Vertex, new GlslCompileOptions(debug: false));
            SpirvCompilationResult fragmentSpirvResult = SpirvCompilation.CompileGlslToSpirv(
                fragmentGlsl, "planet.frag", ShaderStages.Fragment, new GlslCompileOptions(debug: false));

            // Step 2: Cross-compile SPIRV → ESSL (OpenGL ES)
            VertexFragmentCompilationResult result = SpirvCompilation.CompileVertexFragment(
                vertexSpirvResult.SpirvBytes,
                fragmentSpirvResult.SpirvBytes,
                CrossCompileTarget.ESSL,
                new CrossCompileOptions(false, false, normalizeResourceNames: false));

            _output.WriteLine("=== VERTEX (GLSL→SPIRV[debug:false]→ESSL) ===");
            _output.WriteLine(result.VertexShader);
            _output.WriteLine("");
            _output.WriteLine("=== FRAGMENT (GLSL→SPIRV[debug:false]→ESSL) ===");
            _output.WriteLine(result.FragmentShader);
            _output.WriteLine("");

            // Check that uniform block names are preserved in ESSL output
            Assert.Contains("uniform ProjView", result.VertexShader);
            Assert.Contains("uniform LightInfo", result.FragmentShader);
            Assert.DoesNotContain("_RESERVED_IDENTIFIER_FIXUP_", result.VertexShader);
            Assert.DoesNotContain("_RESERVED_IDENTIFIER_FIXUP_", result.FragmentShader);
            Assert.DoesNotContain("vdspv_0_0", result.VertexShader);
        }

        [Fact]
        public void UniformNames_NormalizedInGLSL_WithNormalization()
        {
            byte[] vsBytes = TestUtil.LoadBytes("planet.vert");
            byte[] fsBytes = TestUtil.LoadBytes("planet.frag");

            VertexFragmentCompilationResult result = SpirvCompilation.CompileVertexFragment(
                vsBytes,
                fsBytes,
                CrossCompileTarget.GLSL,
                new CrossCompileOptions(false, false, normalizeResourceNames: true));

            _output.WriteLine("=== VERTEX SHADER OUTPUT (NORMALIZED) ===");
            _output.WriteLine(result.VertexShader);
            _output.WriteLine("");
            _output.WriteLine("=== FRAGMENT SHADER OUTPUT (NORMALIZED) ===");
            _output.WriteLine(result.FragmentShader);
            _output.WriteLine("");

            // Check that names ARE normalized to vdspv_ format
            Assert.Contains("vdspv_0_0", result.VertexShader);
            Assert.Contains("vdspv_0_0", result.FragmentShader);
            Assert.Contains("vdspv_0_2", result.FragmentShader);
        }
    }
}