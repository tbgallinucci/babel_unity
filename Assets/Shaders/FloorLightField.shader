// ============================================================================
//  FloorLightField.shader  —  pass de tela cheia (Etapa 3 de iluminação)
//
//  Soma, em CIMA de qualquer material já renderizado, o "bounce" que o
//  LightFieldBaker propagou pelo grid do WFC (FloorLightField.cs monta a
//  textura e publica _FloorLightField/_FloorLightFieldParams como globais).
//
//  Por que tela cheia e não trocar o shader de cada material: sobrevive à
//  troca de arte de produção sem ninguém precisar lembrar de reportar o efeito
//  pros materiais novos — é o Renderer que aplica, não o material. O preço é
//  reconstruir a posição de mundo a partir da depth buffer aqui, em vez de já
//  ter worldPos de graça como um shader normal tem.
//
//  Desenhado via FullScreenPassRendererFeature (nativo da URP — ver Runtime/
//  RendererFeatures/FullScreenPassRendererFeature.cs no pacote): ela desenha
//  um triângulo só, cobrindo a tela inteira, sem vertex/index buffer nenhum —
//  por isso o vértice vem de Blit.hlsl (GetFullScreenTriangleVertexPosition),
//  não escrito à mão aqui.
//
//  Blend One One (aditivo puro): nunca escurece o que já está na tela, só
//  soma. É por isso que o shader não precisa ler a cor existente — a mistura
//  é feita pelo hardware, mais barato que buscar e somar manualmente.
// ============================================================================

Shader "Babel/FloorLightField"
{
    Properties { }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // Mesmo trio que todo pass de tela cheia da própria URP usa (ver
        // Shaders/Utils/ScreenSpaceAmbientOcclusion.shader): sem isto o triângulo
        // procedural pode sumir dependendo da convenção de winding da plataforma,
        // silenciosamente — nada de erro no Console, só a tela sem o efeito.
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "FloorLightField"
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // ORDEM IMPORTA: Core.hlsl da URP tem que vir ANTES de Blit.hlsl. Blit.hlsl usa
            // TEXTURE2D_X (variante XR-aware) mas não prepara essa macro sozinho — quem faz
            // isso é a cadeia de include do Core.hlsl da URP. Sem ele primeiro: "unrecognized
            // identifier 'TEXTURE2D_X'" em Blit.hlsl(14). É a ordem que o próprio manual da
            // URP documenta pra blit de tela cheia custom.
            //
            // De quebra já traz UNITY_MATRIX_I_VP (a inversa view-projection que
            // ComputeWorldSpacePosition precisa pra desfazer o clip space).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Vert() do triângulo procedural (Attributes/Varyings, GetFullScreenTriangleVertexPosition)
            // e, transitivamente, Common.hlsl — de onde vem ComputeWorldSpacePosition e UNITY_REVERSED_Z.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // SampleSceneDepth(). Já inclui Core.hlsl de novo, mas o include guard cuida disso.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_FloorLightField);
            SAMPLER(sampler_FloorLightField);

            // xy = canto do grid em coordenadas de mundo (AnnotatedGrid.Origin);
            // zw = 1 / (tamanho do grid em metros), pros dois eixos. Publicados por
            // FloorLightField.cs a cada Rebuild — ver o comentário lá pra convenção de UV.
            float4 _FloorLightFieldParams;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float depth = SampleSceneDepth(uv);

                // Sem geometria sob o pixel (void/céu) — plano distante em reversed-Z
                // (padrão na maioria das plataformas modernas) fica em profundidade ~0;
                // sem reversed-Z, ~1. Sem este corte, ComputeWorldSpacePosition devolveria
                // um ponto absurdamente longe e a amostra abaixo cairia fora do grid de
                // qualquer forma — mas sair cedo evita o trabalho à toa.
                #if UNITY_REVERSED_Z
                    if (depth <= 0.0001) return float4(0, 0, 0, 0);
                #else
                    if (depth >= 0.9999) return float4(0, 0, 0, 0);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float2 fieldUV = (worldPos.xz - _FloorLightFieldParams.xy) * _FloorLightFieldParams.zw;

                // Fora do footprint do andar atual (corredor de transição entre andares,
                // void fora do casco, ou simplesmente nenhum andar gerado ainda). A textura
                // está em Clamp — sem este corte explícito, todo pixel fora do grid pegaria
                // esticada a cor da CÉLULA DA BORDA em vez de nada, vazando luz pro infinito
                // em vez de simplesmente não contribuir.
                if (any(fieldUV < 0.0) || any(fieldUV > 1.0))
                    return float4(0, 0, 0, 0);

                float3 bounce = SAMPLE_TEXTURE2D_LOD(_FloorLightField, sampler_FloorLightField, fieldUV, 0).rgb;
                return float4(bounce, 0); // alfa não importa em Blend One One; 0 é o valor neutro
            }
            ENDHLSL
        }
    }
}
