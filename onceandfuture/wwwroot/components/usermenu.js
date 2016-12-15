var React = require('react'); // N.B. Still need this because JSX.
import { connect } from 'react-redux';
import { userMenuToggle } from '../actions';
import {
    COLOR_VERY_DARK,
    COLOR_VERY_LIGHT,
    ICON_FONT_SIZE,
    TEXT_FONT_SIZE,
} from './style';
import Tooltip from './tooltip';

const UserMenuBase = function({visible, onToggle}) {

    // TODO:STYLIFY
    let style = {
        cursor: 'pointer',
        fontSize: 19,
        width: 33,
        height: 33,
        padding: 7,
    };
    let menu = <span />;
    if (visible) {
        style = Object.assign({}, style, {
            backgroundColor: COLOR_VERY_DARK,
            color: COLOR_VERY_LIGHT,
        });

        const menu_style = {
            backgroundColor: COLOR_VERY_DARK,
            color: COLOR_VERY_LIGHT,
            display: 'inline-block',
            fontSize: TEXT_FONT_SIZE,
            fontWeight: 'normal',
            position: 'absolute',
            right: 0,
            textAlign: 'center',
            top: 33,
            width: 120,
            zIndex: 4,
        };

        menu = <div style={menu_style}>
            <p>Option one</p>
            <p>Option two</p>
        </div>;
    }

    return <div onClick={onToggle} style={style}>
        <Tooltip position="bottomleft" tip="View account settings.">
          <i className="fa fa-user" />
        </Tooltip>
        {menu}
    </div>;
};

const mapStateToProps = (state) => {
  return {
    visible: state.user_menu.visible,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
      onToggle: () => dispatch(userMenuToggle()),
  };
};
const UserMenu = connect(mapStateToProps, mapDispatchToProps)(UserMenuBase);

export default UserMenu;