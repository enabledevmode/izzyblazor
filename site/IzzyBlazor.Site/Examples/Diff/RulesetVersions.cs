namespace IzzyBlazor.Site.Examples.Diff;

/// <summary>Four versions of a putaway ruleset, for the version history demo: each a small change on the one before.</summary>
public static class RulesetVersions
{
    public static readonly IReadOnlyList<RulesetVersion> All =
    [
        new(1, "Published", "Ada Lovelace", new DateOnly(2026, 9, 2), "First version", V1),
        new(2, "Published", "Grace Hopper", new DateOnly(2026, 9, 18), "Heavy items on the floor", V2),
        new(3, "Published", "Grace Hopper", new DateOnly(2026, 10, 6), "Slow movers at the back", V3),
        new(4, "Draft", "Alan Turing", new DateOnly(2026, 10, 26), "Staged tasks and the cooler", V4),
    ];

    private const string V1 =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <ruleset xmlns="urn:rulebook:ruleset" id="putaway" name="Putaway" schemaVersion="1">
          <scope site="CLT1"/>
          <constraints>
            <rule id="released" reason="Only released tasks are put away.">
              <condition field="task.hostStatus" op="eq" value="released"/>
            </rule>
            <rule id="no-hazmat-mezzanine" reason="Hazardous goods stay off the mezzanine.">
              <condition field="item.hazmat" op="eq" value="true"/>
              <condition field="location.zone" op="eq" value="MEZZ"/>
            </rule>
          </constraints>
          <bands>
            <band id="fast" reason="Fast movers near the dock.">
              <condition field="item.velocity" op="eq" value="A"/>
            </band>
            <otherwise id="normal" reason="Everything else."/>
          </bands>
        </ruleset>
        """;

    private const string V2 =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <ruleset xmlns="urn:rulebook:ruleset" id="putaway" name="Putaway" schemaVersion="1">
          <scope site="CLT1"/>
          <constraints>
            <rule id="released" reason="Only released tasks are put away.">
              <condition field="task.hostStatus" op="eq" value="released"/>
            </rule>
            <rule id="no-hazmat-mezzanine" reason="Hazardous goods stay off the mezzanine.">
              <condition field="item.hazmat" op="eq" value="true"/>
              <condition field="location.zone" op="eq" value="MEZZ"/>
            </rule>
            <rule id="weight" reason="Heavy items go on the floor.">
              <condition field="item.weightKg" op="gt" value="25"/>
              <condition field="location.level" op="gt" value="1"/>
            </rule>
          </constraints>
          <bands>
            <band id="fast" reason="Fast movers near the dock.">
              <condition field="item.velocity" op="eq" value="A"/>
            </band>
            <otherwise id="normal" reason="Everything else."/>
          </bands>
        </ruleset>
        """;

    private const string V3 =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <ruleset xmlns="urn:rulebook:ruleset" id="putaway" name="Putaway" schemaVersion="1">
          <scope site="CLT1"/>
          <constraints>
            <!-- Released tasks only. -->
            <rule id="released" reason="Only released tasks are put away.">
              <condition field="task.hostStatus" op="eq" value="released"/>
            </rule>
            <rule id="no-hazmat-mezzanine" reason="Hazardous goods stay off the mezzanine.">
              <condition field="item.hazmat" op="eq" value="true"/>
              <condition field="location.zone" op="eq" value="MEZZ"/>
            </rule>
            <rule id="weight" reason="Heavy items go on the floor.">
              <condition field="item.weightKg" op="gt" value="25"/>
              <condition field="location.level" op="gt" value="1"/>
            </rule>
          </constraints>
          <bands>
            <band id="fast" reason="Fast movers near the dock.">
              <condition field="item.velocity" op="eq" value="A"/>
            </band>
            <band id="slow" reason="Slow movers at the back.">
              <condition field="item.velocity" op="in"><value>C</value><value>D</value></condition>
            </band>
            <otherwise id="normal" reason="Everything else."/>
          </bands>
        </ruleset>
        """;

    private const string V4 =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <ruleset xmlns="urn:rulebook:ruleset" id="putaway" name="Putaway" schemaVersion="1">
          <scope site="CLT1"/>
          <constraints>
            <rule id="released" reason="Only released or staged tasks are put away.">
              <condition field="task.hostStatus" op="in"><value>released</value><value>staged</value></condition>
            </rule>
            <rule id="no-hazmat-mezzanine" reason="Hazardous goods stay off the mezzanine.">
              <condition field="item.hazmat" op="eq" value="true"/>
              <condition field="location.zone" op="eq" value="MEZZ"/>
            </rule>
            <rule id="weight" reason="Heavy items go on the floor.">
              <condition field="item.weightKg" op="gt" value="20"/>
              <condition field="location.level" op="gt" value="1"/>
            </rule>
            <rule id="cold" reason="Chilled items stay in the cooler.">
              <condition field="item.storage" op="eq" value="chilled"/>
              <condition field="location.zone" op="ne" value="COOL"/>
            </rule>
          </constraints>
          <bands>
            <band id="fast" reason="Fast movers near the dock.">
              <condition field="item.velocity" op="eq" value="A"/>
            </band>
            <band id="slow" reason="Slow movers at the back.">
              <condition field="item.velocity" op="in"><value>C</value><value>D</value></condition>
            </band>
            <otherwise id="normal" reason="Everything else."/>
          </bands>
        </ruleset>
        """;
}

public sealed record RulesetVersion(int Number, string State, string Author, DateOnly On, string Note, string Xml);
