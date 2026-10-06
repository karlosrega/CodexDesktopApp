using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AdmitOne;

namespace AdmitOne.Desktop
{
    public sealed class OperationViewModel : ViewModel { }
    public static class Ui
    {
        public static readonly Brush Red=new SolidColorBrush(Color.FromRgb(168,36,43));
        public static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(104,107,116));
        public static void Bind(FrameworkElement target,DependencyProperty property,string path) { target.SetBinding(property,new Binding(path)); }
        public static TextBlock Text(string value,double size=14,Brush brush=null,bool bold=false)
        {
            return new TextBlock { Text=value,FontSize=size,Foreground=brush??new SolidColorBrush(Color.FromRgb(36,38,43)),FontWeight=bold ? FontWeights.Bold : FontWeights.Normal,Margin=new Thickness(0,0,0,12) };
        }
        public static Image Logo(double width=180) { return new Image { Source=new BitmapImage(new Uri("pack://application:,,,/AdmitOne.Desktop;component/Assets/logo.png")),Width=width,Height=width/5.25,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,24) }; }
        public static Button Button(string text,Action action,Func<bool> enabled=null,bool secondary=false)
        {
            var b=new Button { Content=text,Command=new ActionCommand(action,enabled) }; if(secondary) b.SetResourceReference(FrameworkElement.StyleProperty,"Secondary"); return b;
        }
        public static Border Card(UIElement content,Thickness padding) { return new Border { Background=Brushes.White,CornerRadius=new CornerRadius(12),Padding=padding,Child=content }; }
        public static TextBlock Notice()
        {
            var b=Text("",13,Red); b.Margin=new Thickness(0,16,0,8); Bind(b,TextBlock.TextProperty,"Notice"); return b;
        }
        public static TextBox Field(Panel panel,string label,string value,int max=120)
        {
            var caption=Text(label,13,Muted,true); caption.Margin=new Thickness(0,12,0,6); panel.Children.Add(caption);
            var input=new TextBox { Text=value??"",MaxLength=max }; panel.Children.Add(input); return input;
        }
        public static void CloseApplicationIfMain(Window window)
        {
            window.Closed+=(s,e)=> { if(ReferenceEquals(Application.Current.MainWindow,window)) Application.Current.Shutdown(); };
        }
        public static Window Dialog(Window owner,string title,double width=540,double height=630)
        {
            var window=new Window { Owner=owner!=null && owner.IsVisible ? owner : null,Title=title,Width=width,Height=height,MinWidth=480,MinHeight=420,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false }; Fit(window); return window;
        }
        public static void Fit(Window window)
        {
            var work=SystemParameters.WorkArea;
            window.MinWidth=Math.Min(window.MinWidth,Math.Max(360,work.Width-32)); window.MinHeight=Math.Min(window.MinHeight,Math.Max(360,work.Height-32));
            window.Width=Math.Min(window.Width,Math.Max(window.MinWidth,work.Width-32)); window.Height=Math.Min(window.Height,Math.Max(window.MinHeight,work.Height-32));
        }
        public static ScrollViewer Scroll(UIElement content) { return new ScrollViewer { Content=content,Background=new SolidColorBrush(Color.FromRgb(245,245,247)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; }
    }
    public sealed class PasswordEntry : Grid
    {
        private readonly PasswordBox secret=new PasswordBox { MaxLength=256 };
        private readonly TextBox visible=new TextBox { MaxLength=256,Visibility=Visibility.Collapsed };
        private readonly CheckBox show=new CheckBox { Content="Mostrar contraseña",FontSize=12 };
        public string Value { get { return show.IsChecked==true ? visible.Text : secret.Password; } }
        public PasswordEntry()
        {
            RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            Children.Add(secret); Children.Add(visible); SetRow(show,1); Children.Add(show);
            show.Checked+=(s,e)=> { visible.Text=secret.Password; secret.Clear(); secret.Visibility=Visibility.Collapsed; visible.Visibility=Visibility.Visible; };
            show.Unchecked+=(s,e)=> { secret.Password=visible.Text; visible.Clear(); visible.Visibility=Visibility.Collapsed; secret.Visibility=Visibility.Visible; };
        }
        public void Clear() { secret.Clear(); visible.Clear(); show.IsChecked=false; }
    }
    public sealed class LoginWindow : Window
    {
        private readonly LoginViewModel vm;
        public FrameworkElement Surface { get { return (FrameworkElement)Content; } }
        public LoginWindow(ApplicationService service)
        {
            vm=new LoginViewModel(service); DataContext=vm;
            Title="Admit One · Iniciar sesión"; Width=1050; Height=680; MinWidth=820; MinHeight=540; WindowStartupLocation=WindowStartupLocation.CenterScreen; Ui.Fit(this);
            var root=new Grid { Background=new SolidColorBrush(Color.FromRgb(245,245,247)) }; root.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(0.46,GridUnitType.Star) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(0.54,GridUnitType.Star) });
            var brand=new Grid { Background=new LinearGradientBrush(Color.FromRgb(125,26,32),Color.FromRgb(184,41,49),45),Margin=new Thickness(0) };
            var brandText=new StackPanel { Margin=new Thickness(48),VerticalAlignment=VerticalAlignment.Center };
            brandText.Children.Add(Ui.Text("TU OPERACIÓN, CONECTADA",12,new SolidColorBrush(Color.FromRgb(246,204,206)),true));
            brandText.Children.Add(Ui.Text("Más control.\nMás posibilidades.",40,Brushes.White,true));
            brandText.Children.Add(Ui.Text("Un espacio para gestionar tu equipo y mantener cada acceso bajo control.",17,new SolidColorBrush(Color.FromRgb(250,223,224))));
            var stripe=new Border { Height=4,Width=48,Background=Brushes.White,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,24,0,24) }; brandText.Children.Add(stripe);
            brandText.Children.Add(Ui.Text("Administración de usuarios y perfiles",13,Brushes.White)); brand.Children.Add(brandText); root.Children.Add(brand);
            var panel=new StackPanel { Margin=new Thickness(44),VerticalAlignment=VerticalAlignment.Center };
            panel.Children.Add(Ui.Logo(210)); panel.Children.Add(Ui.Text("Bienvenido",32,null,true)); panel.Children.Add(Ui.Text("Inicia sesión para continuar a tu administración.",14,Ui.Muted));
            var username=Ui.Field(panel,"Usuario","",64); username.SetBinding(TextBox.TextProperty,new Binding("Username") { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged });
            var label=Ui.Text("Contraseña",13,Ui.Muted,true); label.Margin=new Thickness(0,16,0,6); panel.Children.Add(label);
            var password=new PasswordEntry(); panel.Children.Add(password);
            var login=Ui.Button("Iniciar sesión  →",async()=>
            {
                var session=await vm.Login(password.Value); password.Clear();
                if(session==null) return;
                if(session.MustChangePassword)
                {
                    var change=new PasswordWindow(this,"Actualiza tu contraseña","Para continuar, reemplaza tu contraseña temporal por una de al menos 12 caracteres.",async value=>session=await Task.Run(()=>service.ChangePassword(session,value)));
                    if(change.ShowDialog()!=true)
                    {
                        await vm.Run(()=>Task.Run(()=>service.Logout(session))); vm.Notice="Debes cambiar la contraseña para continuar."; return;
                    }
                }
                ((App)Application.Current).ShowAdministration(session,this);
            },()=>!vm.Busy);
            login.IsDefault=true; login.Margin=new Thickness(0,12,0,0); panel.Children.Add(login); panel.Children.Add(Ui.Notice());
            var busy=Ui.Text("Verificando acceso…",12,Ui.Muted); busy.SetBinding(VisibilityProperty,new Binding("Busy") { Converter=new BooleanToVisibilityConverter() }); panel.Children.Add(busy);
            panel.Children.Add(Ui.Text("Admit One · Cada equipo, el acceso correcto.",12,Ui.Muted));
            var form=Ui.Scroll(panel); Grid.SetColumn(form,1); root.Children.Add(form); Content=root;
            Loaded+=(s,e)=>username.Focus(); Ui.CloseApplicationIfMain(this);
        }
    }
    public sealed class PasswordWindow : Window
    {
        public FrameworkElement Surface { get { return (FrameworkElement)Content; } }
        public PasswordWindow(Window owner,string heading,string explanation,Func<string,Task> save)
        {
            Owner=owner; Title=heading; Width=530; Height=650; MinWidth=480; MinHeight=580; WindowStartupLocation=WindowStartupLocation.CenterOwner; ShowInTaskbar=false; Ui.Fit(this);
            var vm=new OperationViewModel(); DataContext=vm;
            var panel=new StackPanel { Margin=new Thickness(32) }; panel.Children.Add(Ui.Logo(145)); panel.Children.Add(Ui.Text(heading,25,null,true)); panel.Children.Add(Ui.Text(explanation,14,Ui.Muted));
            panel.Children.Add(Ui.Text("Nueva contraseña",13,Ui.Muted,true)); var first=new PasswordEntry(); panel.Children.Add(first);
            panel.Children.Add(Ui.Text("Confirmar contraseña",13,Ui.Muted,true)); var second=new PasswordEntry(); panel.Children.Add(second);
            var actions=new WrapPanel { Margin=new Thickness(0,16,0,0) };
            actions.Children.Add(Ui.Button("Guardar contraseña",async()=>
            {
                bool success=await vm.Run(async()=>
                {
                    if(first.Value!=second.Value) throw new BusinessException("Las contraseñas no coinciden.");
                    Passwords.Validate(first.Value); await save(first.Value);
                });
                if(success) { first.Clear(); second.Clear(); DialogResult=true; }
            },()=>!vm.Busy));
            actions.Children.Add(Ui.Button("Cancelar",()=>DialogResult=false,()=>!vm.Busy,true)); panel.Children.Add(actions); panel.Children.Add(Ui.Notice()); Content=Ui.Scroll(panel);
            Closing+=(s,e)=> { if(vm.Busy) e.Cancel=true; };
        }
    }
    public sealed class AdministrationWindow : Window
    {
        private readonly ApplicationService service;
        public AdministrationViewModel Model { get; private set; }
        private readonly DataGrid table=new DataGrid();
        public FrameworkElement Surface { get { return (FrameworkElement)Content; } }
        public AdministrationWindow(ApplicationService service,Session session)
        {
            this.service=service; Model=new AdministrationViewModel(service,session); DataContext=Model;
            Title="Admit One · Administración"; Width=1200; Height=780; MinWidth=900; MinHeight=540; WindowStartupLocation=WindowStartupLocation.CenterScreen; Ui.Fit(this);
            var root=new Grid { Background=new SolidColorBrush(Color.FromRgb(245,245,247)) }; root.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(238) }); root.ColumnDefinitions.Add(new ColumnDefinition());
            var sidebar=new Grid { Background=Brushes.White }; sidebar.RowDefinitions.Add(new RowDefinition()); sidebar.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var nav=new StackPanel { Margin=new Thickness(24,32,20,24) }; nav.Children.Add(Ui.Logo(180)); nav.Children.Add(Ui.Text("ADMINISTRACIÓN",11,Ui.Muted,true));
            var users=Ui.Button("Usuarios",async()=> { Model.Switch(true); ConfigureColumns(); await Model.Refresh(); },()=>!Model.Busy);
            users.SetBinding(VisibilityProperty,new Binding("CanUsers") { Converter=new BooleanToVisibilityConverter() }); users.Margin=new Thickness(0,12,0,10); nav.Children.Add(users);
            var profiles=Ui.Button("Perfiles",async()=> { Model.Switch(false); ConfigureColumns(); await Model.Refresh(); },()=>!Model.Busy);
            profiles.SetBinding(VisibilityProperty,new Binding("CanProfiles") { Converter=new BooleanToVisibilityConverter() }); profiles.Margin=new Thickness(0,0,0,10); nav.Children.Add(profiles);
            var sideBottom=new StackPanel { Margin=new Thickness(24) }; sideBottom.Children.Add(Ui.Text("Cada equipo,\nel acceso correcto.",18,Ui.Muted,true));
            sideBottom.Children.Add(Ui.Button("Cerrar sesión",async()=>
            {
                if(await Model.Run(()=>Task.Run(()=>service.Logout(Model.Session)))) ((App)Application.Current).EndSession(this);
                else System.Windows.MessageBox.Show(Model.Notice+"\nPuedes cerrar la ventana; la sesión local dejará de estar disponible.","Aviso",MessageBoxButton.OK,MessageBoxImage.Warning);
            },()=>!Model.Busy,true));
            sidebar.Children.Add(nav); Grid.SetRow(sideBottom,1); sidebar.Children.Add(sideBottom); root.Children.Add(sidebar);
            var body=new Grid { Margin=new Thickness(32,28,32,24) }; body.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var top=new DockPanel(); var identity=Ui.Text("",13,Ui.Muted); Ui.Bind(identity,TextBlock.TextProperty,"Identity"); identity.HorizontalAlignment=HorizontalAlignment.Right; DockPanel.SetDock(identity,Dock.Right); top.Children.Add(identity); top.Children.Add(Ui.Text("BACK OFFICE",11,Ui.Red,true)); body.Children.Add(top);
            var headings=new StackPanel { Margin=new Thickness(0,16,0,18) }; var title=Ui.Text("",30,null,true); Ui.Bind(title,TextBlock.TextProperty,"Title"); headings.Children.Add(title); var subtitle=Ui.Text("",14,Ui.Muted); Ui.Bind(subtitle,TextBlock.TextProperty,"Subtitle"); headings.Children.Add(subtitle); Grid.SetRow(headings,1); body.Children.Add(headings);
            var toolbar=new StackPanel { Margin=new Thickness(0,0,0,18) };
            var searchPanel=new DockPanel { Margin=new Thickness(0,0,0,14) };
            var refresh=Ui.Button("Buscar / actualizar",async()=>await Model.Refresh(),()=>!Model.Busy,true); refresh.Margin=new Thickness(12,0,0,0); DockPanel.SetDock(refresh,Dock.Right); searchPanel.Children.Add(refresh);
            var search=new TextBox { MaxLength=120,ToolTip="Buscar por nombre o usuario" }; search.SetBinding(TextBox.TextProperty,new Binding("Search") { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged }); search.KeyDown+=async(s,e)=> { if(e.Key==System.Windows.Input.Key.Enter) await Model.Refresh(); }; searchPanel.Children.Add(search); toolbar.Children.Add(searchPanel);
            var actions=new WrapPanel(); actions.Children.Add(Restricted("+ Nuevo",async()=>await Edit(true),"CanCreate",()=>!Model.Busy)); actions.Children.Add(Restricted("Editar",async()=>await Edit(false),"CanEdit",()=>!Model.Busy && Model.Selected!=null,true)); actions.Children.Add(Restricted("Activar / desactivar",async()=>await Toggle(),"CanDeactivate",()=>!Model.Busy && Model.Selected!=null,true)); actions.Children.Add(Restricted("Restablecer contraseña",async()=>await Reset(),"CanReset",()=>!Model.Busy && Model.Selected!=null,true)); toolbar.Children.Add(actions); Grid.SetRow(toolbar,2); body.Children.Add(toolbar);
            table.SetBinding(ItemsControl.ItemsSourceProperty,new Binding("Rows")); table.SetBinding(DataGrid.SelectedItemProperty,new Binding("Selected") { Mode=BindingMode.TwoWay }); table.Margin=new Thickness(0,0,0,16); ConfigureColumns();
            var card=Ui.Card(table,new Thickness(0)); Grid.SetRow(card,3); body.Children.Add(card);
            var status=new StackPanel(); var count=Ui.Text("",12,Ui.Muted); Ui.Bind(count,TextBlock.TextProperty,"Count"); status.Children.Add(count); status.Children.Add(Ui.Notice()); var busy=Ui.Text("Procesando…",12,Ui.Muted); busy.SetBinding(VisibilityProperty,new Binding("Busy") { Converter=new BooleanToVisibilityConverter() }); status.Children.Add(busy); Grid.SetRow(status,4); body.Children.Add(status);
            Grid.SetColumn(body,1); root.Children.Add(body); Content=root; Ui.CloseApplicationIfMain(this);
            Loaded+=async(s,e)=> { if(Model.CanUsers || Model.CanProfiles) await Model.Refresh(); else Model.Notice="Tu perfil no tiene permisos para consultar estos módulos."; };
        }
        private static Button Restricted(string label,Action action,string permission,Func<bool> enabled,bool secondary=false)
        {
            var b=Ui.Button(label,action,enabled,secondary); b.SetBinding(VisibilityProperty,new Binding(permission) { Converter=new BooleanToVisibilityConverter() }); return b;
        }
        public void ConfigureColumns()
        {
            table.Columns.Clear();
            if(Model.UsersMode) { Column("Usuario","Username",1); Column("Nombre completo","FullName",1.7); Column("Correo","Email",1.7); Column("Perfil","ProfileName",1.3); Column("Estado","State",0.7); Column("Contraseña","PasswordState",1.2); }
            else { Column("Perfil","Name",1.3); Column("Descripción","Description",2.5); Column("Estado","State",0.8); }
        }
        private void Column(string heading,string path,double width)
        {
            var style=new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center)); style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.NoWrap));
            table.Columns.Add(new DataGridTextColumn { Header=heading,Binding=new Binding(path),Width=new DataGridLength(width,DataGridLengthUnitType.Star),MinWidth=100,ElementStyle=style });
        }
        private async Task Edit(bool create)
        {
            if(Model.UsersMode)
            {
                var user=create ? new UserRecord { IsActive=true } : ((UserRecord)Model.Selected).Copy(); List<ProfileRecord> profiles=null;
                if(!await Model.Run(async()=>profiles=await Task.Run(()=>service.Profiles(Model.Session,"",true)))) return;
                var dialog=UserDialog(user,profiles); if(dialog.ShowDialog()==true) await Model.Refresh();
            }
            else
            {
                var profile=create ? new ProfileRecord { IsActive=true } : ((ProfileRecord)Model.Selected).Copy();
                if(ProfileDialog(profile).ShowDialog()==true) await Model.Refresh();
            }
        }
        private Window UserDialog(UserRecord user,List<ProfileRecord> profiles)
        {
            var dialog=Ui.Dialog(this,user.Id==0 ? "Nuevo usuario" : "Editar usuario",550,690); var op=new OperationViewModel(); dialog.DataContext=op;
            var panel=new StackPanel { Margin=new Thickness(30) }; panel.Children.Add(Ui.Text(dialog.Title,26,null,true));
            var username=Ui.Field(panel,"Usuario",user.Username,64); var fullName=Ui.Field(panel,"Nombre completo",user.FullName,120); var email=Ui.Field(panel,"Correo electrónico (opcional)",user.Email,254);
            panel.Children.Add(Ui.Text("Perfil",13,Ui.Muted,true)); var profile=new ComboBox { ItemsSource=profiles.Where(p=>p.IsActive || p.Id==user.ProfileId).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id" }; if(user.ProfileId!=0) profile.SelectedValue=user.ProfileId; panel.Children.Add(profile);
            var active=new CheckBox { Content="Usuario activo",IsChecked=user.IsActive,IsEnabled=user.Id==0 || Model.Session.Can(Permission.UsersDeactivate) }; panel.Children.Add(active);
            PasswordEntry password=null;
            if(user.Id==0) { panel.Children.Add(Ui.Text("Contraseña temporal · mínimo 12 caracteres",13,Ui.Muted,true)); password=new PasswordEntry(); panel.Children.Add(password); }
            var actions=new WrapPanel { Margin=new Thickness(0,16,0,0) };
            actions.Children.Add(Ui.Button("Guardar usuario",async()=>
            {
                user.Username=username.Text; user.FullName=fullName.Text; user.Email=email.Text; user.IsActive=active.IsChecked==true;
                if(profile.SelectedValue==null) { op.Notice="Selecciona un perfil."; return; } user.ProfileId=(int)profile.SelectedValue;
                string initial=password?.Value;
                if(await op.Run(()=>Task.Run(()=>service.SaveUser(Model.Session,user,initial)))) { password?.Clear(); dialog.DialogResult=true; }
            },()=>!op.Busy)); actions.Children.Add(Ui.Button("Cancelar",()=>dialog.DialogResult=false,()=>!op.Busy,true)); panel.Children.Add(actions); panel.Children.Add(Ui.Notice()); dialog.Content=Ui.Scroll(panel); dialog.Closing+=(s,e)=> { if(op.Busy) e.Cancel=true; }; return dialog;
        }
        private Window ProfileDialog(ProfileRecord profile)
        {
            var dialog=Ui.Dialog(this,profile.Id==0 ? "Nuevo perfil" : "Editar perfil",650,620); var op=new OperationViewModel(); dialog.DataContext=op;
            var panel=new StackPanel { Margin=new Thickness(30) }; panel.Children.Add(Ui.Text(dialog.Title,26,null,true)); var name=Ui.Field(panel,"Nombre del perfil",profile.Name,80); var description=Ui.Field(panel,"Descripción",profile.Description,250);
            var active=new CheckBox { Content="Perfil activo",IsChecked=profile.IsActive,IsEnabled=profile.Id==0 || Model.Session.Can(Permission.ProfilesDeactivate) }; panel.Children.Add(active); panel.Children.Add(Ui.Text("Permisos por módulo",18,null,true));
            var permissions=new Grid(); for(int i=0;i<5;i++) permissions.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(i==0 ? 1.3 : 1,GridUnitType.Star) }); for(int i=0;i<3;i++) permissions.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var labels=new[]{"Módulo","Consultar","Crear","Editar","Activar /\ndesactivar"}; for(int i=0;i<5;i++) { var t=Ui.Text(labels[i],12,Ui.Muted,true); Grid.SetColumn(t,i); permissions.Children.Add(t); }
            var checks=new Dictionary<Permission,CheckBox>();
            for(int row=1;row<=2;row++)
            {
                var module=Ui.Text(row==1 ? "Usuarios" : "Perfiles",13,null,true); module.VerticalAlignment=VerticalAlignment.Center; Grid.SetRow(module,row); permissions.Children.Add(module);
                for(int col=1;col<=4;col++) { var bit=(Permission)(1<<((row-1)*4+col-1)); var check=new CheckBox { IsChecked=(profile.Permissions & bit)==bit,HorizontalAlignment=HorizontalAlignment.Center,ToolTip=(row==1 ? "Usuarios: " : "Perfiles: ")+labels[col],Content="" }; Grid.SetRow(check,row); Grid.SetColumn(check,col); permissions.Children.Add(check); checks.Add(bit,check); }
            }
            panel.Children.Add(permissions); panel.Children.Add(Ui.Text("Selecciona únicamente los accesos necesarios para este equipo.",12,Ui.Muted)); var actions=new WrapPanel { Margin=new Thickness(0,16,0,0) };
            actions.Children.Add(Ui.Button("Guardar perfil",async()=>
            {
                profile.Name=name.Text; profile.Description=description.Text; profile.IsActive=active.IsChecked==true; profile.Permissions=Permission.None;
                foreach(var check in checks) if(check.Value.IsChecked==true) profile.Permissions|=check.Key;
                if(await op.Run(()=>Task.Run(()=>service.SaveProfile(Model.Session,profile)))) dialog.DialogResult=true;
            },()=>!op.Busy)); actions.Children.Add(Ui.Button("Cancelar",()=>dialog.DialogResult=false,()=>!op.Busy,true)); panel.Children.Add(actions); panel.Children.Add(Ui.Notice()); dialog.Content=Ui.Scroll(panel); dialog.Closing+=(s,e)=> { if(op.Busy) e.Cancel=true; }; return dialog;
        }
        private async Task Toggle()
        {
            if(System.Windows.MessageBox.Show(this,"¿Deseas cambiar el estado del registro seleccionado?","Cambiar estado",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            object selected=Model.Selected;
            bool success=await Model.Run(()=>Task.Run(()=> { if(selected is UserRecord) { var user=(UserRecord)selected; service.SetUserActive(Model.Session,user,!user.IsActive); } else { var profile=(ProfileRecord)selected; service.SetProfileActive(Model.Session,profile,!profile.IsActive); } }));
            if(success) await Model.Refresh();
        }
        private async Task Reset()
        {
            var user=(UserRecord)Model.Selected;
            var dialog=new PasswordWindow(this,"Restablecer contraseña","Define una contraseña temporal. El usuario deberá cambiarla al volver a entrar.",value=>Task.Run(()=>service.ResetPassword(Model.Session,user,value)));
            if(dialog.ShowDialog()==true) await Model.Refresh();
        }
    }
}
