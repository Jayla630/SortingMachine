#!/bin/bash
# =========================================================
# File: coverage.sh
# Project: SortingMachine
# Sprint: S6 | Agent: Gemini CLI
# =========================================================

# coverage.sh - 一键生成测试覆盖率报告
set -e

REPORT_DIR="coverage-report"
COVERAGE_OUT="coverage-results"

echo "=== 运行测试 + 收集覆盖率 ==="
rm -rf $COVERAGE_OUT $REPORT_DIR

dotnet test SortingMachine.sln \
    --collect:"XPlat Code Coverage" \
    --results-directory $COVERAGE_OUT \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo ""
echo "=== 生成 HTML 报告 ==="

if ! command -v reportgenerator &> /dev/null; then
    echo "安装 ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
fi

COVERAGE_FILE=$(find $COVERAGE_OUT -name "coverage.cobertura.xml" | head -1)

reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORT_DIR" \
    -reporttypes:"Html;HtmlSummary;Badges" \
    -assemblyfilters:"+SortingMachine" \
    -classfilters:"-*Mock*;-*Tests*;-*Fixture*" \
    -title:"SortingMachine 覆盖率报告"

echo ""
echo "=== 完成 ==="
echo "报告位置：$REPORT_DIR/index.html"
