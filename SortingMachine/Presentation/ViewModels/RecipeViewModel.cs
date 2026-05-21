// =========================================================
// File: Presentation/ViewModels/RecipeViewModel.cs
// Project: SortingMachine
// Sprint: S3 | Agent: Gemini CLI
// =========================================================
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Mvvm;
using Microsoft.Extensions.Logging;
using SortingMachine.Domain.Recipe;

namespace SortingMachine.Presentation.ViewModels;

public class RecipeViewModel : BindableBase
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly ILogger<RecipeViewModel> _logger;

    private SortingRecipe? _selectedRecipe;
    public SortingRecipe? SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (SetProperty(ref _selectedRecipe, value))
            {
                ActivateCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                DuplicateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private SortingRecipe? _activeRecipe;
    public SortingRecipe? ActiveRecipe
    {
        get => _activeRecipe;
        set => SetProperty(ref _activeRecipe, value);
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoadRecipesCommand.RaiseCanExecuteChanged();
                CreateDefaultCommand.RaiseCanExecuteChanged();
                ActivateCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                DuplicateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<SortingRecipe> Recipes { get; } = new();

    public DelegateCommand LoadRecipesCommand { get; }
    public DelegateCommand CreateDefaultCommand { get; }
    public DelegateCommand<SortingRecipe> ActivateCommand { get; }
    public DelegateCommand<SortingRecipe> DeleteCommand { get; }
    public DelegateCommand<SortingRecipe> DuplicateCommand { get; }

    public RecipeViewModel(IRecipeRepository recipeRepository, ILogger<RecipeViewModel> logger)
    {
        _recipeRepository = recipeRepository;
        _logger = logger;

        LoadRecipesCommand = new DelegateCommand(async () => await ExecuteLoadRecipes(), () => !IsBusy);
        CreateDefaultCommand = new DelegateCommand(async () => await ExecuteCreateDefault(), () => !IsBusy);
        ActivateCommand = new DelegateCommand<SortingRecipe>(async (r) => await ExecuteActivate(r), (r) => !IsBusy && r != null);
        DeleteCommand = new DelegateCommand<SortingRecipe>(async (r) => await ExecuteDelete(r), (r) => !IsBusy && r != null);
        DuplicateCommand = new DelegateCommand<SortingRecipe>(async (r) => await ExecuteDuplicate(r), (r) => !IsBusy && r != null);

        // 初始化加载
        _ = ExecuteLoadRecipes();
    }

    private async Task ExecuteLoadRecipes()
    {
        IsBusy = true;
        StatusText = "正在加载配方...";
        try
        {
            var list = await _recipeRepository.GetAllAsync();
            Recipes.Clear();
            foreach (var item in list)
            {
                Recipes.Add(item);
            }
            ActiveRecipe = Recipes.FirstOrDefault(r => r.IsActive);
            StatusText = $"已加载 {Recipes.Count} 个配方";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recipes");
            StatusText = "加载配方失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteCreateDefault()
    {
        IsBusy = true;
        StatusText = "正在创建默认配方...";
        try
        {
            var newRecipe = new SortingRecipe
            {
                ProductModel = "NEW-PRODUCT",
                Description = "新建配方"
            };
            await _recipeRepository.SaveAsync(newRecipe);
            await ExecuteLoadRecipes();
            SelectedRecipe = Recipes.FirstOrDefault(r => r.RecipeId == newRecipe.RecipeId);
            StatusText = "默认配方已创建";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default recipe");
            StatusText = "创建配方失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteActivate(SortingRecipe? recipe)
    {
        if (recipe == null) return;
        IsBusy = true;
        StatusText = $"正在激活配方: {recipe.ProductModel}...";
        try
        {
            await _recipeRepository.SetActiveAsync(recipe.RecipeId);
            await ExecuteLoadRecipes();
            StatusText = $"已激活：{recipe.ProductModel}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate recipe {RecipeId}", recipe.RecipeId);
            StatusText = "激活配方失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteDelete(SortingRecipe? recipe)
    {
        if (recipe == null) return;
        if (recipe.IsActive)
        {
            StatusText = "无法删除正在激活中的配方";
            return;
        }

        IsBusy = true;
        StatusText = $"正在删除配方: {recipe.ProductModel}...";
        try
        {
            await _recipeRepository.DeleteAsync(recipe.RecipeId);
            await ExecuteLoadRecipes();
            StatusText = "配方已删除";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete recipe {RecipeId}", recipe.RecipeId);
            StatusText = "删除配方失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteDuplicate(SortingRecipe? recipe)
    {
        if (recipe == null) return;
        IsBusy = true;
        StatusText = $"正在复制配方: {recipe.ProductModel}...";
        try
        {
            var newRecipe = recipe with 
            { 
                RecipeId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                ProductModel = $"{recipe.ProductModel} (副本)",
                IsActive = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _recipeRepository.SaveAsync(newRecipe);
            await ExecuteLoadRecipes();
            SelectedRecipe = Recipes.FirstOrDefault(r => r.RecipeId == newRecipe.RecipeId);
            StatusText = $"配方已复制: {newRecipe.ProductModel}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate recipe {RecipeId}", recipe.RecipeId);
            StatusText = "复制配方失败";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
