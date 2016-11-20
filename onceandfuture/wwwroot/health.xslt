<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

<xsl:template match="/">
    <html>
        <head>
            <style>
            .healthBox {
                border-style: solid;
                border-width: 1px;
                margin: 3px;
            }

            .healthLog {
                padding: 5px;   
                font-family: monospace;  
            }

            h2 { margin: 0px; }
            </style>
        </head>
        <body>
            <h1>Health Results</h1>
            <xsl:apply-templates />
        </body>
    </html>
</xsl:template>

<xsl:template match="runtimeSummary">
    <div class="healthBox">
        <h2>Runtime Configuration</h2>
        <hr />
        <table>
            <xsl:for-each select="runtimeProperty">
                <tr><td><xsl:value-of select="@name" /></td><td><xsl:value-of select="@value" /></td></tr>
            </xsl:for-each>
        </table>
    </div>
</xsl:template>

<xsl:template match="healthResult">
    <div class="healthBox">
        <xsl:element name="div">
            <xsl:if test="healthy = 'true'">
                <xsl:attribute name="style">
                    background-color: green;
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="healthy = 'false'">
                <xsl:attribute name="style">
                    background-color: red;
                </xsl:attribute>
            </xsl:if>
            <h2><xsl:value-of select="title" /></h2>
        </xsl:element>
        <div class="healthLog">
            <xsl:for-each select="log/p">
                <xsl:value-of select="." /><br/>
            </xsl:for-each>
        </div>
    </div>
</xsl:template>

</xsl:stylesheet>